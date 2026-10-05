import os
import gc
import ctypes
from datetime import datetime as Datetime
import numpy as np
import pandas as pd
import tensorflow as tf
from tensorflow.keras import layers, models

psapi = ctypes.WinDLL("psapi")
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

def trim_working_set():
    psapi.EmptyWorkingSet(kernel32.GetCurrentProcess())

# -----------------------------------------------------------------------------
# Shopping NN v2
# Input:
#   5  StrategyAI output
#   5  own fraction distribution / synergy context
#   6 x 24 card features:
#      exists, price_ratio_to_money, free_effect, 12 effect one-hot,
#      attack, health, money (normalized), 5 fraction one-hot, synergy         
#   My health, Enemy health, MyDeckValue, EnemyDeckValue
# Total = 5 + 5 + 144 + 4 = 158
# Output = 6 policy logits.
# Training: IQL (Implicit Q-Learning) + advantage-weighted behavior cloning.
# Inference: softmax(logits / temperature) only for a percentage-like ranking.
#
# KÉT BETÖLTÉSI ÚT:
#   - load_experience() + train_q_model()            -> kisebb CSV-khez (RAM)
#   - build_memmap_dataset() + train_q_model_streaming() -> Sok adat (Disk)

# Kostrikov et al. (2021) IQL
# -----------------------------------------------------------------------------

INPUT_DIM = 158
N_ACTIONS = 6
GAMMA = 0.98
BATCH_SIZE = 512
EPOCHS = 5 #20
# IQL hyperparameters
IQL_EXPECTILE = 0.7
IQL_BETA = 0.5
IQL_WEIGHT_CLIP = 100.0

# Reward shaping
USE_REWARD_SHAPING = False

TEMPERATURE = 1.0

# -----------------------------------------------------------------------------
# Potenciál-alapú jutalom-formázás (reward shaping)
#
# A hálózat bemenetei csak a döntéssorozat végén adnak jutalomat +1/-1
#
# Minden döntéshez adunk egy KIS, AZONNALI jelzést -- KÉT
# forrásból:
#   1) életerő-előny változása
#   2) PAKLI-ÉRTÉK változása
#
# Ng, Harada & Russell (1999, "Policy invariance under reward transformations", ICML) 
# -----------------------------------------------------------------------------
HEALTH_MY_IDX = INPUT_DIM - 4
HEALTH_ENEMY_IDX = INPUT_DIM - 3
DECKVALUE_MY_IDX = INPUT_DIM - 2
DECKVALUE_ENEMY_IDX = INPUT_DIM - 1

HEALTH_POTENTIAL_SCALE = 90.0      # kb. a kezdő életerő (InitHealth)
DECKVALUE_POTENTIAL_SCALE = 400.0  # kb. a maximális paklipontszám (40 meccsre)

HEALTH_SHAPING_STRENGTH = 0.3     # 0.0 = kikapcsolva
DECKVALUE_SHAPING_STRENGTH = 0.7  # 0.0 = kikapcsolva
# -----------------------------------------------------------------------------

SLOT_FEATURE_DIM = 24
SLOT_BLOCK_START = 10  # 5 strategy + 5 synergy

META_COLS = [
    "episode", "decision", "actor", "money",
    "my_health", "enemy_health", "my_deck_value", "enemy_deck_value"
]
FEATURE_COLS = [
    f"strategy_{i}" for i in range(5)
] + [
    f"synergy_fraction_{i}" for i in range(5)
] + [
    f"slot{s}_{name}"
    for s in range(6)
    for name in (
        ["exists", "price_ratio", "free_effect"]
        + [f"effect_{e}" for e in range(12)]
        + ["attack", "health", "money"]
        + [f"fraction_{f}" for f in range(5)]
        + ["synergy"]     
    )
] + META_COLS[4:]
TARGET_COLS = ["action", "reward", "done"]

assert len(FEATURE_COLS) == INPUT_DIM, len(FEATURE_COLS)

EXIST_COLS = [f"slot{s}_exists" for s in range(6)]                                                  


def _keep_mask(df_or_chunk):
    n_exist = df_or_chunk[EXIST_COLS].sum(axis=1)
    return (n_exist > 1) | (df_or_chunk["done"] == 1)


def build_policy_model() -> tf.keras.Model:
    inputs = layers.Input(shape=(INPUT_DIM,))
    x = layers.Dense(256, activation="leaky_relu")(inputs)
    x = layers.LayerNormalization()(x)
    x = layers.Dense(256, activation="leaky_relu")(x)
    shared = layers.Dense(128, activation="leaky_relu")(x)
    x = layers.Dense(64, activation="leaky_relu")(shared)
    outputs = layers.Dense(N_ACTIONS, activation=None, name="policy_logits")(x)
    return models.Model(inputs, outputs, name="shopping_policy")

def build_q_model() -> tf.keras.Model:
    inputs = layers.Input(shape=(INPUT_DIM,))
    x = layers.Dense(256, activation="leaky_relu")(inputs)
    x = layers.LayerNormalization()(x)
    x = layers.Dense(256, activation="leaky_relu")(x)
    x = layers.Dense(128, activation="leaky_relu")(x)
    x = layers.Dense(64, activation="leaky_relu")(x)
    outputs = layers.Dense(N_ACTIONS, activation=None, name="q_values")(x)
    return models.Model(inputs, outputs, name="q_network")

def build_value_model() -> tf.keras.Model:
    inputs = layers.Input(shape=(INPUT_DIM,))
    x = layers.Dense(256, activation="leaky_relu")(inputs)
    x = layers.LayerNormalization()(x)
    x = layers.Dense(256, activation="leaky_relu")(x)
    x = layers.Dense(128, activation="leaky_relu")(x)
    x = layers.Dense(64, activation="leaky_relu")(x)
    outputs = layers.Dense(1, activation=None, name="value")(x)
    return models.Model(inputs, outputs, name="value_network")

def masked_probabilities(model, features: np.ndarray, temperature: float = TEMPERATURE):
    x = np.asarray(features, dtype=np.float32).reshape(1, -1)
    logits = model.predict(x, verbose=0)[0]
    slot_part = features[SLOT_BLOCK_START:SLOT_BLOCK_START + 6 * SLOT_FEATURE_DIM]
    exists = np.array([slot_part[slot * SLOT_FEATURE_DIM] for slot in range(6)], dtype=np.float32)
    masked = np.where(exists > 0.5, logits, -1e9)
    masked = masked / max(temperature, 1e-6)
    masked -= np.max(masked)
    probs = np.exp(masked)
    probs /= np.sum(probs)
    return logits, probs

def masked_probabilities_batch(model, features_batch, temperature=TEMPERATURE):
    logits = model.predict(features_batch, verbose=0)
    slot_part = features_batch[:, SLOT_BLOCK_START:SLOT_BLOCK_START + 6 * SLOT_FEATURE_DIM]
    exists = slot_part.reshape(-1, 6, SLOT_FEATURE_DIM)[:, :, 0] > 0.5
    masked = np.where(exists, logits, -1e9)
    masked = masked / max(temperature, 1e-6)
    masked -= np.max(masked, axis=1, keepdims=True)
    probs = np.exp(masked)
    probs /= np.sum(probs, axis=1, keepdims=True)
    return logits, probs

def ranking_from_model(model, features: np.ndarray):
    scores, probs = masked_probabilities(model, features)
    order = np.argsort(probs)[::-1]
    return order.tolist(), scores.tolist(), probs.tolist()

def diagnose_policy_spread(model, features_mm, n_samples=200, seed=0):
    rng = np.random.default_rng(seed)
    n = features_mm.shape[0]
    sample_idx = rng.choice(n, size=min(n_samples, n), replace=False)
    batch_features = np.asarray(features_mm[sample_idx])
    logits, probs = masked_probabilities_batch(model, batch_features)
    spreads = []
    all_valid = []
    for i, features in enumerate(batch_features):
        exists = np.array([features[SLOT_BLOCK_START + slot * SLOT_FEATURE_DIM] > 0.5 for slot in range(6)])
        vals = logits[i][exists]
        if len(vals) > 1:
            spreads.append(vals.max() - vals.min())
        all_valid.extend(vals.tolist())
    spreads = np.asarray(spreads)
    all_valid = np.asarray(all_valid)
    print("\n=== Policy diagnosztika ===")
    print(f"Minták száma: {len(sample_idx)}")
    print(f"Egy állapoton BELÜLI logit spread: átlag={spreads.mean():.4f}, min={spreads.min():.4f}, max={spreads.max():.4f}")
    print(f"Globális logit std: {all_valid.std():.4f}")
    print(f"Logit tartomány: [{all_valid.min():.4f}, {all_valid.max():.4f}]")
    print("===========================\n")

def _potential(x_batch):
    """
       - HEALTH_SHAPING_STRENGTH * (saját életerő - ellenfél életereje)
       - DECKVALUE_SHAPING_STRENGTH * (saját pakli-érték - ellenfél pakli-értéke)
    """
    my_health = x_batch[:, HEALTH_MY_IDX]
    enemy_health = x_batch[:, HEALTH_ENEMY_IDX]
    my_deck_value = x_batch[:, DECKVALUE_MY_IDX]
    enemy_deck_value = x_batch[:, DECKVALUE_ENEMY_IDX]

    health_term = HEALTH_SHAPING_STRENGTH * (my_health - enemy_health)
    deckvalue_term = DECKVALUE_SHAPING_STRENGTH * (my_deck_value - enemy_deck_value)
    return health_term + deckvalue_term

def pairwise_ranking_loss(logits,actions,exists,advantage,margin=1.0):
    chosen_logits = tf.gather(
        logits,
        actions,
        axis=1,
        batch_dims=1
    )
    action_ids = tf.range(6, dtype=tf.int32)
    not_chosen = tf.not_equal(action_ids[None, :],actions[:, None])
    pair_mask = exists & not_chosen
    diff = chosen_logits[:, None] - logits
    pair_losses = tf.maximum(0.0,margin - diff)
    pair_losses *= tf.cast(pair_mask, tf.float32)
    n_pairs = tf.reduce_sum(tf.cast(pair_mask, tf.float32),axis=1)
    loss_per_sample = (tf.reduce_sum(pair_losses, axis=1)/tf.maximum(n_pairs, 1.0))
    weights = tf.exp(
        tf.clip_by_value(
            advantage / IQL_BETA,
            -20.0,
            tf.math.log(IQL_WEIGHT_CLIP)
        )
    )
    return tf.reduce_mean(weights * loss_per_sample)
    
def _normalize_context_features(x):
    x[:, HEALTH_MY_IDX] /= HEALTH_POTENTIAL_SCALE
    x[:, HEALTH_ENEMY_IDX] /= HEALTH_POTENTIAL_SCALE
    x[:, DECKVALUE_MY_IDX] /= DECKVALUE_POTENTIAL_SCALE
    x[:, DECKVALUE_ENEMY_IDX] /= DECKVALUE_POTENTIAL_SCALE
    return x

def _slot_exists_mask(x_batch):
    slot_part = x_batch[:, SLOT_BLOCK_START:SLOT_BLOCK_START + N_ACTIONS * SLOT_FEATURE_DIM]
    slots = tf.reshape(slot_part, [-1, N_ACTIONS, SLOT_FEATURE_DIM])
    return slots[:, :, 0] > 0.5

def _expectile_loss(diff, expectile):
    weight = tf.where(diff < 0.0, 1.0 - expectile, expectile)
    return weight * tf.square(diff)

def _make_iql_train_step(q1, q2, value, policy, q1_opt, q2_opt, value_opt, policy_opt, huber):
    input_signature = [
        tf.TensorSpec(shape=[None, INPUT_DIM], dtype=tf.float32),
        tf.TensorSpec(shape=[None], dtype=tf.int32),
        tf.TensorSpec(shape=[None], dtype=tf.float32),
        tf.TensorSpec(shape=[None], dtype=tf.float32),
        tf.TensorSpec(shape=[None, INPUT_DIM], dtype=tf.float32),
        tf.TensorSpec(shape=[None], dtype=tf.float32),
    ]

    @tf.function(input_signature=input_signature)
    def train_step(bx, ba, br, bd, bnext, bhas_next):
        if USE_REWARD_SHAPING:
            br = br + GAMMA * _potential(bnext) * bhas_next - _potential(bx)

        next_v = tf.squeeze(value(bnext, training=False), axis=1)
        q_target = br + GAMMA * next_v * (1.0 - bd) * bhas_next

        with tf.GradientTape(persistent=True) as tape:
            q1_all = q1(bx, training=True)
            q2_all = q2(bx, training=True)
            q1_chosen = tf.gather(q1_all, ba, axis=1, batch_dims=1)
            q2_chosen = tf.gather(q2_all, ba, axis=1, batch_dims=1)
            q1_loss = tf.reduce_mean(huber(q_target, q1_chosen))
            q2_loss = tf.reduce_mean(huber(q_target, q2_chosen))
        q1_grads = tape.gradient(q1_loss, q1.trainable_variables)
        q2_grads = tape.gradient(q2_loss, q2.trainable_variables)
        q1_opt.apply_gradients(zip(q1_grads, q1.trainable_variables))
        q2_opt.apply_gradients(zip(q2_grads, q2.trainable_variables))
        del tape

        q_min = tf.minimum(q1_all, q2_all)
        q_taken_min = tf.gather(q_min, ba, axis=1, batch_dims=1)
        with tf.GradientTape() as tape:
            v = tf.squeeze(value(bx, training=True), axis=1)
            diff = q_taken_min - v
            v_loss = tf.reduce_mean(_expectile_loss(diff, IQL_EXPECTILE))
        v_grads = tape.gradient(v_loss, value.trainable_variables)
        value_opt.apply_gradients(zip(v_grads, value.trainable_variables))

        with tf.GradientTape() as tape:
            logits = policy(bx, training=True)
            exists = _slot_exists_mask(bx)
            masked_logits = tf.where(exists, logits, tf.constant(-1e9, tf.float32))
            log_probs = tf.nn.log_softmax(masked_logits, axis=1)
            chosen_log_prob = tf.gather(log_probs, ba, axis=1, batch_dims=1)
            advantage = tf.stop_gradient(q_taken_min - v)
            weights = tf.exp(tf.clip_by_value(advantage / IQL_BETA, -20.0, tf.math.log(IQL_WEIGHT_CLIP)))
            policy_loss = -tf.reduce_mean(weights * chosen_log_prob) # + 0.2 * pairwise_ranking_loss(logits, ba, exists, advantage)
        policy_grads = tape.gradient(policy_loss, policy.trainable_variables)
        policy_opt.apply_gradients(zip(policy_grads, policy.trainable_variables))

        return q1_loss + q2_loss, v_loss, policy_loss

    return train_step

def _augment_batch_permutation(bx, ba, rng, n_permutations=1):
    n = len(bx)
    if n_permutations <= 0:
        return bx, ba

    slot_part = bx[:, SLOT_BLOCK_START:SLOT_BLOCK_START + 6 * SLOT_FEATURE_DIM]
    slots = slot_part.reshape(n, 6, SLOT_FEATURE_DIM)

    x_parts = [bx]
    a_parts = [ba]

    for _ in range(n_permutations):
        perms = np.argsort(rng.random((n, 6)), axis=1)
        permuted_slots = np.take_along_axis(slots, perms[:, :, None], axis=1)

        new_bx = bx.copy()
        new_bx[:, SLOT_BLOCK_START:SLOT_BLOCK_START + 6 * SLOT_FEATURE_DIM] = permuted_slots.reshape(n, -1)
        new_ba = np.argmax(perms == ba[:, None], axis=1).astype(np.int32)

        x_parts.append(new_bx)
        a_parts.append(new_ba)

    return np.concatenate(x_parts, axis=0), np.concatenate(a_parts, axis=0)

# =============================================================================
# 1. CSV --> Minden a RAM ban
# =============================================================================

def load_experience(paths):
    if isinstance(paths, (str, os.PathLike)):
        paths = [paths]

    frames = []
    episode_offset = 0
    for path in paths:
        if not os.path.exists(path):
            raise FileNotFoundError(path)
        df_part = pd.read_csv(path)
        missing = [c for c in META_COLS + FEATURE_COLS + TARGET_COLS if c not in df_part.columns]
        if missing:
            raise ValueError(f"Missing CSV columns in {path}: {missing}")

        df_part = df_part.copy()
        df_part["episode"] = df_part["episode"] + episode_offset
        frames.append(df_part)
        episode_offset = df_part["episode"].max() + 1

    df = pd.concat(frames, ignore_index=True)
    df = df.sort_values(["episode", "actor", "decision"]).reset_index(drop=True)

    keep = _keep_mask(df).to_numpy()
    n_before = len(df)
    df = df[keep].reset_index(drop=True)
    print(f"[load_experience] szures: {len(df)}/{n_before} sor maradt "
          f"({len(df) / n_before:.1%}) -- csak valodi dontesek + done=1 sorok")                                    

    x = df[FEATURE_COLS].to_numpy(np.float32)
    x = _normalize_context_features(x)                                                                                         
    actions = df["action"].to_numpy(np.int32)
    rewards = df["reward"].to_numpy(np.float32)
    done = df["done"].to_numpy(np.float32)

    next_x = np.zeros_like(x)
    has_next = np.zeros(len(df), dtype=np.float32)
    groups = df.groupby(["episode", "actor"], sort=False).indices
    for _, idx in groups.items():
        idx = np.asarray(idx, dtype=np.int64)
        if len(idx) > 1:
            next_x[idx[:-1]] = x[idx[1:]]
            has_next[idx[:-1]] = 1.0

    return df, x, actions, rewards, done, next_x, has_next


def augment_slot_permutations(x, actions, rewards, done, next_x, has_next, n_permutations=2, seed=0):
    rng = np.random.default_rng(seed)
    x_aug, actions_aug = _augment_batch_permutation(x, actions, rng, n_permutations=n_permutations)
 
    repeat = n_permutations + 1
    rewards_aug = np.tile(rewards, repeat)
    done_aug = np.tile(done, repeat)
    next_x_aug = np.tile(next_x, (repeat, 1))
    has_next_aug = np.tile(has_next, repeat)
 
    return x_aug, actions_aug, rewards_aug, done_aug, next_x_aug, has_next_aug


def _train_iql_loop(features_getter, n, actions, rewards, done, next_getter, augment_permutations=0, seed=42, block_shuffle_batches=0):
    q1 = build_q_model()
    q2 = build_q_model()
    value = build_value_model()
    policy = build_policy_model()

    opt1 = tf.keras.optimizers.AdamW(learning_rate=3e-4, weight_decay=1e-5, clipnorm=1.0)
    opt2 = tf.keras.optimizers.AdamW(learning_rate=3e-4, weight_decay=1e-5, clipnorm=1.0)
    optv = tf.keras.optimizers.AdamW(learning_rate=3e-4, weight_decay=1e-5, clipnorm=1.0)
    optp = tf.keras.optimizers.AdamW(learning_rate=3e-4, weight_decay=1e-5, clipnorm=1.0)
    huber = tf.keras.losses.Huber(delta=0.5, reduction="none")
    train_step = _make_iql_train_step(q1, q2, value, policy, opt1, opt2, optv, optp, huber)

    rng = np.random.default_rng(seed)
    for epoch in range(EPOCHS):
        startime = Datetime.now()
        indices = _make_shuffled_indices(n, rng, block_shuffle_batches=block_shuffle_batches)
        losses = []
        for start in range(0, n, BATCH_SIZE):
            batch_idx = indices[start:start + BATCH_SIZE]
            bx = np.asarray(features_getter(batch_idx), dtype=np.float32)
            next_ids_or_x = next_getter(batch_idx)
            if isinstance(next_ids_or_x, tuple):
                bnext, bhas_next = next_ids_or_x
            else:
                bnext = np.asarray(next_ids_or_x, dtype=np.float32)
                bhas_next = np.ones(len(batch_idx), dtype=np.float32)

            ba = np.asarray(actions[batch_idx], dtype=np.int32)
            br = np.asarray(rewards[batch_idx], dtype=np.float32)
            bd = np.asarray(done[batch_idx], dtype=np.float32)

            if augment_permutations > 0:
                bx, ba = _augment_batch_permutation(bx, ba, rng, n_permutations=augment_permutations)
                repeat = augment_permutations + 1
                br = np.tile(br, repeat)
                bd = np.tile(bd, repeat)
                bnext = np.tile(bnext, (repeat, 1))
                bhas_next = np.tile(bhas_next, repeat)

            out = train_step(tf.convert_to_tensor(bx, tf.float32), tf.convert_to_tensor(ba, tf.int32),
                             tf.convert_to_tensor(br, tf.float32), tf.convert_to_tensor(bd, tf.float32),
                             tf.convert_to_tensor(bnext, tf.float32), tf.convert_to_tensor(bhas_next, tf.float32))
            qloss, vloss, ploss = [float(v) for v in out]
            losses.append(qloss + vloss + ploss)
            print(f"epoch={epoch + 1:02d} || batch={start // BATCH_SIZE + 1:04d} / {n // BATCH_SIZE + 1:04d} || Q={qloss:.5f} || V={vloss:.5f} || policy={ploss:.5f}", end="\r")
            del bx, bnext, ba, br, bd, bhas_next
            if (start // BATCH_SIZE) % 2500 == 0:
                gc.collect()
                trim_working_set()

        endtime = Datetime.now()
        print(f"\nepoch={epoch + 1:02d} || total_loss={np.mean(losses):.6f} || runtime={(endtime - startime).total_seconds() / 60:.2f} min")
        diagnose_policy_spread(policy, features_getter(np.arange(min(n, 200000))))
        gc.collect()
        trim_working_set()

    policy.export("F:\\MC\\TENSORS")
    return policy

def train_q_model(df, x, actions, rewards, done, next_x, has_next):
    n = len(x)
    return _train_iql_loop(lambda idx: x[idx], n, actions, rewards, done,
                           lambda idx: (next_x[idx], has_next[idx]),
                           augment_permutations=0)


# =============================================================================
# 2. MEMMAP >>> Disk
# =============================================================================

def _count_csv_rows(path):
    with open(path, "rb") as f:
        return sum(1 for _ in f) - 1  # -1 a fejléc miatt


def _count_kept_rows(path, chunk_rows=200_000):
    usecols = EXIST_COLS + ["done"]
    total = 0
    kept = 0
    for chunk in pd.read_csv(path, usecols=usecols, chunksize=chunk_rows):
        total += len(chunk)
        kept += int(_keep_mask(chunk).sum())
    return kept, total


def _build_next_row_index(episodes, actors, decisions):
    order = np.lexsort((decisions, actors, episodes))
    sorted_ep = episodes[order]
    sorted_actor = actors[order]

    same_group = (sorted_ep[:-1] == sorted_ep[1:]) & (sorted_actor[:-1] == sorted_actor[1:])

    next_idx = np.full(len(episodes), -1, dtype=np.int64)
    next_idx[order[:-1][same_group]] = order[1:][same_group]
    return next_idx


def build_memmap_dataset(paths, out_dir="shopping_memmap", chunk_rows=200_000):
    if isinstance(paths, (str, os.PathLike)):
        paths = [paths]

    os.makedirs(out_dir, exist_ok=True)

    print("1/3 -- sorok szamlalasa ...")
    counts = [_count_kept_rows(p, chunk_rows=chunk_rows) for p in paths]
    kept_counts = [k for k, _ in counts]
    raw_counts = [t for _, t in counts]
    total_rows = sum(kept_counts)                                    
    total_raw = sum(raw_counts)
    print(f"     osszesen {total_rows} sor marad meg a(z) {total_raw} eredeti sorbol "
          f"({total_rows / total_raw:.1%}), {len(paths)} fajlbol")

    features_path = os.path.join(out_dir, "features.npy")
    features_mm = np.lib.format.open_memmap(
        features_path, mode="w+", dtype=np.float32, shape=(total_rows, INPUT_DIM)
    )

    episodes = np.empty(total_rows, dtype=np.int64)
    actors = np.empty(total_rows, dtype=np.int8)
    decisions = np.empty(total_rows, dtype=np.int32)
    actions = np.empty(total_rows, dtype=np.int32)
    rewards = np.empty(total_rows, dtype=np.float32)
    done = np.empty(total_rows, dtype=np.float32)

    print("2/3 -- CSV-k beolvasasa es irasa a memmap-be...")
    write_pos = 0
    episode_offset = 0
    for path, n_rows in zip(paths, raw_counts):
        max_episode_seen = -1
        first_chunk = True
        for chunk in pd.read_csv(path, chunksize=chunk_rows):
            if first_chunk:
                missing = [c for c in META_COLS + FEATURE_COLS + TARGET_COLS if c not in chunk.columns]
                if missing:
                    raise ValueError(f"Missing CSV columns in {path}: {missing}")
                first_chunk = False

            chunk_episode_full = chunk["episode"].to_numpy(np.int64) + episode_offset
            max_episode_seen = max(max_episode_seen, int(chunk_episode_full.max()))

            keep = _keep_mask(chunk).to_numpy()
            chunk = chunk[keep]
            chunk_episode = chunk_episode_full[keep]                                                                         
            n = len(chunk)
            if n == 0:
                continue

            chunk_features = chunk[FEATURE_COLS].to_numpy(np.float32)
            chunk_features = _normalize_context_features(chunk_features)
            features_mm[write_pos:write_pos + n] = chunk_features
            episodes[write_pos:write_pos + n] = chunk_episode
            actors[write_pos:write_pos + n] = chunk["actor"].to_numpy(np.int8)
            decisions[write_pos:write_pos + n] = chunk["decision"].to_numpy(np.int32)
            actions[write_pos:write_pos + n] = chunk["action"].to_numpy(np.int32)
            rewards[write_pos:write_pos + n] = chunk["reward"].to_numpy(np.float32)
            done[write_pos:write_pos + n] = chunk["done"].to_numpy(np.float32)
            write_pos += n
            print(f"     ...{write_pos}/{total_rows} sor feldolgozva", end="\r")

        episode_offset = max_episode_seen + 1

    features_mm.flush()
    print(f"\n     kesz: {write_pos} sor a memmap-ben ({features_path})")
    assert write_pos == total_rows, (
        f"Elteres a szamolt ({total_rows}) es a tenylegesen irt ({write_pos}) sorok kozott!")                                 

    print("3/3 -- (episode, actor) szerinti kovetkezo-dontes index epitese...")
    next_row_index = _build_next_row_index(episodes, actors, decisions)

    np.savez(
        os.path.join(out_dir, "meta.npz"),
        actions=actions, rewards=rewards, done=done, next_row_index=next_row_index,
    )
    print(f"     metaadat elmentve: {out_dir}/meta.npz")
    return out_dir


def load_memmap_dataset(mm_dir="shopping_memmap"):
    features_mm = np.load(os.path.join(mm_dir, "features.npy"), mmap_mode="r")
    meta = np.load(os.path.join(mm_dir, "meta.npz"))
    return features_mm, meta["actions"], meta["rewards"], meta["done"], meta["next_row_index"]


def _make_shuffled_indices(n, rng, block_shuffle_batches=0):
    """A batch-sorrend legenerálása egy epoch-hoz.

    block_shuffle_batches=0 -> TELJESEN véletlen (Disk-en lassú)
    block_shuffle_batches=K -> BLOKKOK sorrendjét keveri. (K*BatchSize)
    """
    if block_shuffle_batches <= 0:
        idx = np.arange(n)
        rng.shuffle(idx)
        return idx

    block_size = BATCH_SIZE * block_shuffle_batches
    n_blocks = (n + block_size - 1) // block_size
    block_order = rng.permutation(n_blocks)
    idx = np.empty(n, dtype=np.int64)
    write_pos = 0
    for b in block_order:
        start = int(b) * block_size
        end = min(start + block_size, n)
        local = np.arange(start, end)
        rng.shuffle(local)
        idx[write_pos:write_pos + len(local)] = local
        write_pos += len(local)
    return idx


def train_q_model_streaming(features_mm, actions, rewards, done, next_row_index,
                             augment_permutations=0, seed=42, block_shuffle_batches=0):
    n = features_mm.shape[0]

    def get_x(idx):
        return np.asarray(features_mm[idx], dtype=np.float32)

    def get_next(idx):
        next_ids = next_row_index[idx]
        valid = next_ids >= 0
        bx = np.asarray(features_mm[idx], dtype=np.float32)
        bnext = np.zeros_like(bx)
        if np.any(valid):
            bnext[valid] = np.asarray(features_mm[next_ids[valid]], dtype=np.float32)
        return bnext, valid.astype(np.float32)

    return _train_iql_loop(get_x, n, actions, rewards, done, get_next,
                           augment_permutations=augment_permutations, seed=seed,
                           block_shuffle_batches=block_shuffle_batches)

if __name__ == "__main__":
    USE_MEMMAP = True

    CSV_PATHS = [
        "data\\shopping_experience_1.csv",
        "data\\shopping_experience_2.csv",
        "data\\shopping_experience_3.csv",
        "data\\shopping_experience_4.csv",
        "data\\shopping_experience_5.csv",
        "data\\shopping_experience_6.csv",
        "data\\shopping_experience_7.csv",
        "data\\shopping_experience_8.csv",
        "data\\shopping_experience_9.csv",
    ]
    AUGMENT_PERMUTATIONS = 0  # 0 = kikapcsolva
    K_block_shuffle_batches = 32

    if USE_MEMMAP:
        MM_DIR = "shopping_memmap"
        if not os.path.exists(os.path.join(MM_DIR, "features.npy")):
            build_memmap_dataset(CSV_PATHS, out_dir=MM_DIR)
        else:
            print(f"Már létezik memmap itt: {MM_DIR} -- ha új CSV-kből "
                  f"szeretnéd újraépíteni, töröld ezt a mappát.")

        features_mm, actions, rewards, done, next_row_index = load_memmap_dataset(MM_DIR)
        print(f"rows={features_mm.shape[0]} input_dim={features_mm.shape[1]}")

        model = train_q_model_streaming(
            features_mm, actions, rewards, done, next_row_index,
            augment_permutations=AUGMENT_PERMUTATIONS, block_shuffle_batches=K_block_shuffle_batches
        )

        order, scores, probs = ranking_from_model(model, np.asarray(features_mm[0]))
        diagnose_policy_spread(model, features_mm)                                           
    else:
        df, x, actions, rewards, done, next_x, has_next = load_experience(CSV_PATHS)
        print(f"rows={len(df)} input_dim={x.shape[1]}")
        print(f"games={df.groupby(['episode', 'actor']).ngroups} (epizód x szereplő)")
        print(f"actions observed={sorted(df['action'].unique().tolist())}")

        if AUGMENT_PERMUTATIONS > 0:
            x, actions, rewards, done, next_x, has_next = augment_slot_permutations(
                x, actions, rewards, done, next_x, has_next, n_permutations=AUGMENT_PERMUTATIONS
            )
            print(f"augmentálás után: rows={len(x)}")

        model = train_q_model(df, x, actions, rewards, done, next_x, has_next)
        order, scores, probs = ranking_from_model(model, x[0])
        diagnose_policy_spread(model, x)                                 

    print("ranking:", order)
    print("scores:", np.round(scores, 3))
    print("probs:", np.round(probs, 3))
