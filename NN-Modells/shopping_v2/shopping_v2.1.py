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
# Output = 6 action values (Q-like scores).
# Inference: softmax(scores / temperature) only for a percentage-like ranking.
#
# KÉT BETÖLTÉSI ÚT:
#   - load_experience() + train_q_model()            -> kisebb CSV-khez (RAM)
#   - build_memmap_dataset() + train_q_model_streaming() -> Sok adat (Disk)
# -----------------------------------------------------------------------------

INPUT_DIM = 158
N_ACTIONS = 6
GAMMA = 0.98
BATCH_SIZE = 512
EPOCHS = 30 #20
TAU = 0.0015 # Soft Update
TEMPERATURE = 0.03

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
DECKVALUE_POTENTIAL_SCALE = 400.0  # kb. a maximális paklipontszám

HEALTH_SHAPING_STRENGTH = 0.3     # 0.0 = kikapcsolva ###0.3
DECKVALUE_SHAPING_STRENGTH = 0.7  # 0.0 = kikapcsolva ###0.7
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


def build_model() -> tf.keras.Model:
    """
    Dueling DQN (Wang et al., 2016, "Dueling Network Architectures for
    Deep Reinforcement Learning").

    Kozos torzs -> szetvalik ket agra:
      - V(s):        1 skalar, "mennyire jo maga az allapot"
      - A(s,a):      N_ACTIONS ertek, "mennyivel jobb/rosszabb EZ az
                     action a tobbi elerheto actionnel osszehasonlitva,
                     EBBEN az allapotban"

    Q(s,a) = V(s) + (A(s,a) - mean_a A(s,a))
    """   
    inputs = layers.Input(shape=(INPUT_DIM,))
    x = layers.Dense(512, activation="silu")(inputs) # 512 silu
    x = layers.LayerNormalization()(x)
    x = layers.Dense(512, activation="silu")(x) # 512 silu
    shared = layers.Dense(256, activation="silu")(x) #128 silu
    # --- Value stream: V(s) ---
    v = layers.Dense(64, activation="silu")(shared) #64 silu
    value = layers.Dense(1, activation=None, name="value")(v)
    # --- Advantage stream: A(s,a) ---                                  
    a = layers.Dense(128, activation="silu")(shared) #128 silu
    advantage = layers.Dense(N_ACTIONS, activation=None, name="advantage")(a)

    def _combine(tensors):
        value_t, advantage_t = tensors
        advantage_mean = tf.reduce_mean(advantage_t, axis=1, keepdims=True)
        return value_t + (advantage_t - advantage_mean)

    outputs = layers.Lambda(_combine, name="q_values")([value, advantage])
    return models.Model(inputs, outputs)

def masked_probabilities(model, features: np.ndarray, temperature: float = TEMPERATURE):
    x = np.asarray(features, dtype=np.float32).reshape(1, -1)
    scores = model.predict(x, verbose=0)[0]

    slot_part = features[SLOT_BLOCK_START:SLOT_BLOCK_START + 6 * SLOT_FEATURE_DIM]
    exists = np.array([
        slot_part[slot * SLOT_FEATURE_DIM]
        for slot in range(6)
    ], dtype=np.float32)

    masked = np.where(exists > 0.5, scores, -1e9)
    logits = masked / max(temperature, 1e-6)
    logits -= np.max(logits)
    probs = np.exp(logits)
    probs = probs / np.sum(probs)
    return scores, probs

def masked_probabilities_batch(model, features_batch, temperature=TEMPERATURE):
    scores = model.predict(features_batch, verbose=0)

    slot_part = features_batch[:, SLOT_BLOCK_START:SLOT_BLOCK_START + 6 * SLOT_FEATURE_DIM]
    slot_part = slot_part.reshape(-1, 6, SLOT_FEATURE_DIM)
    exists = slot_part[:, :, 0] > 0.5

    masked = np.where(exists, scores, -1e9)

    logits = masked / max(temperature, 1e-6)
    logits -= np.max(logits, axis=1, keepdims=True)
    probs = np.exp(logits)
    probs = probs / np.sum(probs, axis=1, keepdims=True)
    return scores, probs

def ranking_from_model(model, features: np.ndarray):
    scores, probs = masked_probabilities(model, features)
    order = np.argsort(probs)[::-1]
    return order.tolist(), scores.tolist(), probs.tolist()


def diagnose_q_value_spread(model, features_mm, n_samples=200, seed=0):
    rng = np.random.default_rng(seed)
    n = features_mm.shape[0]
    sample_idx = rng.choice(n, size=min(n_samples, n), replace=False)

    batch_features = np.asarray(features_mm[sample_idx])
    all_scores, all_probs = masked_probabilities_batch(model, batch_features)

    within_state_spreads = []
    all_valid_scores = []

    for i in range(len(batch_features)):
        scores = all_scores[i]
        features = batch_features[i]
        exists_mask = np.array([
            features[SLOT_BLOCK_START + slot * SLOT_FEATURE_DIM] > 0.5
            for slot in range(6)
        ])
        valid_scores = scores[exists_mask]
        if len(valid_scores) > 1:
            within_state_spreads.append(valid_scores.max() - valid_scores.min())
        all_valid_scores.extend(valid_scores.tolist())

    within_state_spreads = np.array(within_state_spreads)
    all_valid_scores = np.array(all_valid_scores)

    print("\n=== Q-érték diagnosztika ===")
    print(f"Minták száma: {len(sample_idx)}")
    print(f"Egy állapoton BELÜLI szórás (max-min a valódi lapok közt): "
          f"átlag={within_state_spreads.mean():.4f}, "
          f"min={within_state_spreads.min():.4f}, max={within_state_spreads.max():.4f}")
    print(f"Az összes minta Q-értékének globális szórása (std): {all_valid_scores.std():.4f}")
    print(f"Az összes minta Q-értékének tartománya: [{all_valid_scores.min():.4f}, {all_valid_scores.max():.4f}]")

    if within_state_spreads.mean() < 0.01:
        print("[!] FIGYELEM: az egy-állapoton-belüli szórás nagyon kicsi -- "
              "a háló valószínűleg nem tud különbséget tenni a lapok között.")
    if all_valid_scores.std() < 0.01:
        print("[!] FIGYELEM: a globális szórás nagyon kicsi -- a háló "
              "valószínűleg majdnem figyelmen kívül hagyja a bemenetet "
              "(közel-konstans függvényt tanult).")
    if within_state_spreads.mean() >= 0.01 and all_valid_scores.std() >= 0.01:
        print("[OK] A háló érdemben különböző Q-értékeket ad különböző "
              "lapokra és állapotokra.")
    print("=============================\n")

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


def _mask_invalid_slots(raw_q, exists_mask):
    any_exists = tf.reduce_any(exists_mask, axis=1, keepdims=True)
    neg_inf = tf.fill(tf.shape(raw_q), tf.constant(-1e9, dtype=raw_q.dtype))
    masked = tf.where(exists_mask, raw_q, neg_inf)
    return tf.where(any_exists, masked, raw_q)


def _make_train_step(model, target_model, optimizer, huber):
    input_signature = [
        tf.TensorSpec(shape=[None, INPUT_DIM], dtype=tf.float32),   # bx
        tf.TensorSpec(shape=[None], dtype=tf.int32),                 # ba
        tf.TensorSpec(shape=[None], dtype=tf.float32),                # br
        tf.TensorSpec(shape=[None], dtype=tf.float32),                # bd
        tf.TensorSpec(shape=[None, INPUT_DIM], dtype=tf.float32),   # bnext
        tf.TensorSpec(shape=[None], dtype=tf.float32),                # bhas_next
    ]

    @tf.function(input_signature=input_signature)
    def train_step(bx, ba, br, bd, bnext, bhas_next):
        phi_current = _potential(bx)
        phi_next = _potential(bnext)
        shaped_reward = br + GAMMA * phi_next * bhas_next - phi_current

        next_exists = _slot_exists_mask(bnext)

        next_q_online_raw = model(bnext, training=False)
        next_q_online = _mask_invalid_slots(next_q_online_raw, next_exists)
        next_actions = tf.argmax(next_q_online, axis=1, output_type=tf.int32)

        next_q_target_raw = target_model(bnext, training=False)
        next_q_target = _mask_invalid_slots(next_q_target_raw, next_exists)
        next_q = tf.gather(next_q_target, next_actions, axis=1, batch_dims=1)
        target = shaped_reward + GAMMA * next_q * (1.0 - bd) * bhas_next

        with tf.GradientTape() as tape:
            q_values = model(bx, training=True)
            chosen_q = tf.gather(q_values, ba, axis=1, batch_dims=1)
            loss = tf.reduce_mean(huber(target, chosen_q))

        grads = tape.gradient(loss, model.trainable_variables)
        optimizer.apply_gradients(zip(grads, model.trainable_variables))
        return loss

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


def train_q_model(df, x, actions, rewards, done, next_x, has_next):
    model = build_model()
    target_model = build_model()
    target_model.set_weights(model.get_weights())

    optimizer = tf.keras.optimizers.AdamW(learning_rate=3e-4, weight_decay=1e-5, clipnorm=1.0)
    huber = tf.keras.losses.Huber(delta=1.0, reduction="none")

    n = len(x)
    if n < 2:
        raise ValueError("Not enough shopping experience rows.")

    indices = np.arange(n)
    rng = np.random.default_rng(42)

    for epoch in range(EPOCHS):
        startime = Datetime.now()
        rng.shuffle(indices)
        losses = []

        for start in range(0, n, BATCH_SIZE):
            batchstarttime = Datetime.now()
            batch_idx = indices[start:start + BATCH_SIZE]
            bx = tf.convert_to_tensor(x[batch_idx], tf.float32)
            ba = tf.convert_to_tensor(actions[batch_idx], tf.int32)
            br = tf.convert_to_tensor(rewards[batch_idx], tf.float32)
            bd = tf.convert_to_tensor(done[batch_idx], tf.float32)
            bnext = tf.convert_to_tensor(next_x[batch_idx], tf.float32)
            bhas_next = tf.convert_to_tensor(has_next[batch_idx], tf.float32)

            next_exists = _slot_exists_mask(bnext)
            next_q_online_raw = model(bnext, training=False)
            next_q_online = _mask_invalid_slots(next_q_online_raw, next_exists)
            next_actions = tf.argmax(next_q_online, axis=1, output_type=tf.int32)
            next_q_target_raw = target_model(bnext, training=False)
            next_q_target = _mask_invalid_slots(next_q_target_raw, next_exists)
            next_q = tf.gather(next_q_target, next_actions, axis=1, batch_dims=1)
            phi_current = _potential(bx)
            phi_next = _potential(bnext)
            shaped_reward = br + GAMMA * phi_next * bhas_next - phi_current
            target = shaped_reward + GAMMA * next_q * (1.0 - bd) * bhas_next

            with tf.GradientTape() as tape:
                q_values = model(bx, training=True)
                chosen_q = tf.gather(q_values, ba, axis=1, batch_dims=1)
                loss = tf.reduce_mean(huber(target, chosen_q))

            grads = tape.gradient(loss, model.trainable_variables)
            optimizer.apply_gradients(zip(grads, model.trainable_variables))
            losses.append(float(loss))
            batchendtime = Datetime.now()
            print (f"epoch={epoch + 1:02d} || batch={start // BATCH_SIZE + 1:04d} / {n // BATCH_SIZE + 1:04d} || loss={float(loss):.6f} || batch_runtime={(batchendtime - batchstarttime).total_seconds():.2f} sec", end="\r")
            #for target_weight, main_weight in zip(target_model.weights, model.weights):
            #    target_weight.assign(TAU * main_weight + (1.0 - TAU) * target_weight)
            if (start // BATCH_SIZE) % 2500 == 0:
                target_model.set_weights(model.get_weights())
                gc.collect()
                trim_working_set()
        
        target_model.set_weights(model.get_weights())
        endtime = Datetime.now()
        print(f"epoch={epoch + 1:02d} || loss={np.mean(losses):.6f} || runtime={(endtime - startime).total_seconds() / 60} min || remaining={((EPOCHS - epoch - 1) * (endtime - startime)).total_seconds() / 60} min")
        diagnose_q_value_spread(model, x)

    model.export("F:\\MC\\TENSORS")
    return model


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
                             augment_permutations=2, seed=42, block_shuffle_batches=0):
    model = build_model()
    target_model = build_model()
    target_model.set_weights(model.get_weights())

    optimizer = tf.keras.optimizers.AdamW(learning_rate=5e-5, weight_decay=1e-5, clipnorm=1.0)
    huber = tf.keras.losses.Huber(delta=1.0, reduction="none")
    train_step = _make_train_step(model, target_model, optimizer, huber)
    n = features_mm.shape[0]
    if n < 2:
        raise ValueError("Not enough shopping experience rows.")

    rng = np.random.default_rng(seed)

    for epoch in range(EPOCHS):
        startime = Datetime.now()
        indices = _make_shuffled_indices(n, rng, block_shuffle_batches=block_shuffle_batches)
        losses = []

        for start in range(0, n, BATCH_SIZE):
            batchstarttime = Datetime.now()
            batch_idx = indices[start:start + BATCH_SIZE]

            bx = np.asarray(features_mm[batch_idx], dtype=np.float32)

            next_ids = next_row_index[batch_idx]
            valid = next_ids >= 0
            bnext = np.zeros_like(bx)
            if np.any(valid):
                bnext[valid] = np.asarray(features_mm[next_ids[valid]], dtype=np.float32)
            bhas_next = valid.astype(np.float32)

            ba = actions[batch_idx]
            br = rewards[batch_idx]
            bd = done[batch_idx]
            
            if augment_permutations > 0:
                bx, ba = _augment_batch_permutation(bx, ba, rng, n_permutations=augment_permutations)
                repeat = augment_permutations + 1
                br = np.tile(br, repeat)
                bd = np.tile(bd, repeat)
                bnext = np.tile(bnext, (repeat, 1))
                bhas_next = np.tile(bhas_next, repeat)

            loss = train_step(
                tf.convert_to_tensor(bx, tf.float32),
                tf.convert_to_tensor(ba, tf.int32),
                tf.convert_to_tensor(br, tf.float32),
                tf.convert_to_tensor(bd, tf.float32),
                tf.convert_to_tensor(bnext, tf.float32),
                tf.convert_to_tensor(bhas_next, tf.float32),
            )
            losses.append(float(loss))
            batchendtime = Datetime.now()
            print (f"epoch={epoch + 1:02d} || batch={start // BATCH_SIZE + 1:04d} / {n // BATCH_SIZE + 1:04d} || loss={float(loss):.6f} || batch_runtime={(batchendtime - batchstarttime).total_seconds():.2f} sec", end="\r")
            for target_weight, main_weight in zip(target_model.weights, model.weights):
               target_weight.assign(TAU * main_weight + (1.0 - TAU) * target_weight)
            del bx, bnext, ba, br, bd, bhas_next
            if (start // BATCH_SIZE) % 2500 == 0:
                #target_model.set_weights(model.get_weights())
                gc.collect()
                trim_working_set()
        
        #target_model.set_weights(model.get_weights())
        endtime = Datetime.now()
        print(f"epoch={epoch + 1:02d} || loss={np.mean(losses):.6f} || runtime={(endtime - startime).total_seconds() / 60} min || remaining={((EPOCHS - epoch - 1) * (endtime - startime)).total_seconds() / 60} min", end="\n")
        diagnose_q_value_spread(model, features_mm)
        gc.collect()
        trim_working_set()

    model.export("F:\\MC\\TENSORS")
    return model

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
        diagnose_q_value_spread(model, features_mm)                                           
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
        diagnose_q_value_spread(model, x)                                 

    print("ranking:", order)
    print("scores:", np.round(scores, 3))
    print("probs:", np.round(probs, 3))
