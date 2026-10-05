import os
import gc
import ctypes
import platform
from datetime import datetime as Datetime
import numpy as np
import pandas as pd
import tensorflow as tf
from tensorflow.keras import layers, models

if platform.system() == "Windows":
    psapi = ctypes.WinDLL("psapi")
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

    def trim_working_set():
        psapi.EmptyWorkingSet(kernel32.GetCurrentProcess())
else:
    def trim_working_set():
        pass

# -----------------------------------------------------------------------------
# Shopping NN v3 -- KONTEXTUÁLIS RANGSOROLÁS
#
# ARCHITEKTÚRA: megosztott súlyú ("Siamese") kártya-értékelő. Egyetlen kis
# háló f(kontextus, kártya) -> pontszám fut le mind a 6 slotra, azonos
# súlyokkal
#
# Input:
#   5  StrategyAI output
#   5  own fraction distribution / synergy context
#   6 x 24 card features:
#      exists, price_ratio_to_money, free_effect, 12 effect one-hot,
#      attack, health, money (normalized), 5 fraction one-hot, synergy
#   My health, Enemy health, MyDeckValue, EnemyDeckValue
# Total = 5 + 5 + 144 + 4 = 158
# Output = 6 pontszám
# -----------------------------------------------------------------------------

INPUT_DIM = 158
N_ACTIONS = 6
BATCH_SIZE = 512
EPOCHS = 5

SLOT_FEATURE_DIM = 24
SLOT_BLOCK_START = 10   # 5 strategy + 5 synergy
CONTEXT_FRONT_DIM = 10  # 5 strategy + 5 synergy_fraction
CONTEXT_BACK_DIM = 4    # my_health, enemy_health, my_deck_value, enemy_deck_value

PAIRWISE_LOSS_WEIGHT = 1.0  # 0.0 = csak pointwise CE
PAIRWISE_MARGIN = 1.0       # mennyivel kell a választottnak jobbnak lennie a többinél
VAL_FRACTION = 0.002         # validációs adatok száma %

HEALTH_MY_IDX = INPUT_DIM - 4
HEALTH_ENEMY_IDX = INPUT_DIM - 3
DECKVALUE_MY_IDX = INPUT_DIM - 2
DECKVALUE_ENEMY_IDX = INPUT_DIM - 1
HEALTH_POTENTIAL_SCALE = 90.0
DECKVALUE_POTENTIAL_SCALE = 400.0

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

assert len(FEATURE_COLS) == INPUT_DIM, len(FEATURE_COLS)

EXIST_COLS = [f"slot{s}_exists" for s in range(6)]


def _keep_mask(df_or_chunk):
    n_exist = df_or_chunk[EXIST_COLS].sum(axis=1)
    return n_exist > 1


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


# =============================================================================
# Megosztott súlyú ("Siamese") kártya-értékelő
# =============================================================================

def build_card_scorer() -> tf.keras.Model:
    inputs = layers.Input(shape=(CONTEXT_FRONT_DIM + CONTEXT_BACK_DIM + SLOT_FEATURE_DIM,))
    x = layers.Dense(256, activation="silu")(inputs)
    x = layers.LayerNormalization()(x)
    x = layers.Dense(128, activation="silu")(x)
    x = layers.Dense(64, activation="silu")(x)
    out = layers.Dense(1, activation=None, name="card_score")(x)
    return models.Model(inputs, out, name="card_scorer")


def build_policy_model() -> tf.keras.Model:
    full_input = layers.Input(shape=(INPUT_DIM,))

    context_front = layers.Lambda(lambda x: x[:, :CONTEXT_FRONT_DIM],
                                   output_shape=(CONTEXT_FRONT_DIM,))(full_input)
    context_back = layers.Lambda(lambda x: x[:, -CONTEXT_BACK_DIM:],
                                  output_shape=(CONTEXT_BACK_DIM,))(full_input)
    context = layers.Concatenate()([context_front, context_back])

    scorer = build_card_scorer()

    scores = []
    for slot in range(6):
        start = SLOT_BLOCK_START + slot * SLOT_FEATURE_DIM
        card_block = layers.Lambda(
            lambda x, s=start: x[:, s:s + SLOT_FEATURE_DIM],
            output_shape=(SLOT_FEATURE_DIM,)
        )(full_input)
        combined = layers.Concatenate()([context, card_block])
        scores.append(scorer(combined))

    outputs = layers.Concatenate(name="slot_scores")(scores)
    return models.Model(full_input, outputs, name="shopping_policy")


# =============================================================================
# Inferencia
# =============================================================================

def masked_probabilities(model, features: np.ndarray, temperature: float = 1.0):
    x = np.asarray(features, dtype=np.float32).reshape(1, -1)
    scores = model.predict(x, verbose=0)[0]
    slot_part = features[SLOT_BLOCK_START:SLOT_BLOCK_START + 6 * SLOT_FEATURE_DIM]
    exists = np.array([slot_part[slot * SLOT_FEATURE_DIM] for slot in range(6)], dtype=np.float32)
    masked = np.where(exists > 0.5, scores, -1e9)
    masked = masked / max(temperature, 1e-6)
    masked -= np.max(masked)
    probs = np.exp(masked)
    probs /= np.sum(probs)
    return scores, probs


def masked_probabilities_batch(model, features_batch, temperature=1.0):
    scores = model.predict(features_batch, verbose=0)
    slot_part = features_batch[:, SLOT_BLOCK_START:SLOT_BLOCK_START + 6 * SLOT_FEATURE_DIM]
    exists = slot_part.reshape(-1, 6, SLOT_FEATURE_DIM)[:, :, 0] > 0.5
    masked = np.where(exists, scores, -1e9)
    masked = masked / max(temperature, 1e-6)
    masked -= np.max(masked, axis=1, keepdims=True)
    probs = np.exp(masked)
    probs /= np.sum(probs, axis=1, keepdims=True)
    return scores, probs


def ranking_from_model(model, features: np.ndarray):
    scores, probs = masked_probabilities(model, features)
    order = np.argsort(probs)[::-1]
    return order.tolist(), scores.tolist(), probs.tolist()


def diagnose_policy(model, features_mm, actions, idx=None, n_samples=2000, seed=0):
    rng = np.random.default_rng(seed)
    if idx is None:
        idx = np.arange(features_mm.shape[0])
    sample_idx = rng.choice(idx, size=min(n_samples, len(idx)), replace=False)
    batch_features = np.asarray(features_mm[sample_idx])
    batch_actions = np.asarray(actions[sample_idx])
    scores, probs = masked_probabilities_batch(model, batch_features)

    spreads = []
    all_valid = []
    for i, features in enumerate(batch_features):
        exists = np.array([features[SLOT_BLOCK_START + slot * SLOT_FEATURE_DIM] > 0.5 for slot in range(6)])
        vals = scores[i][exists]
        if len(vals) > 1:
            spreads.append(vals.max() - vals.min())
        all_valid.extend(vals.tolist())
    spreads = np.asarray(spreads)
    all_valid = np.asarray(all_valid)

    pred_actions = np.argmax(probs, axis=1)
    top1_acc = float((pred_actions == batch_actions).mean())

    print("\n=== Policy diagnosztika ===")
    print(f"Minták száma: {len(sample_idx)}")
    print(f"Egy állapoton BELÜLI pontszám-spread: átlag={spreads.mean():.4f}, min={spreads.min():.4f}, max={spreads.max():.4f}")
    print(f"Globális pontszám std: {all_valid.std():.4f}  (arány: {spreads.mean() / max(all_valid.std(), 1e-9):.1%})")
    print(f"Top-1 pontosság a heurisztika választásához képest: {top1_acc:.1%}")
    print("===========================\n")
    return top1_acc


# =============================================================================
# Veszteség + train step
# =============================================================================

def _make_train_step(model, optimizer):
    input_signature = [
        tf.TensorSpec(shape=[None, INPUT_DIM], dtype=tf.float32),
        tf.TensorSpec(shape=[None], dtype=tf.int32),
    ]

    @tf.function(input_signature=input_signature)
    def train_step(bx, ba):
        exists = _slot_exists_mask(bx)
        action_ids = tf.range(N_ACTIONS, dtype=tf.int32)
        not_chosen = tf.not_equal(action_ids[None, :], ba[:, None])
        pair_mask = tf.logical_and(exists, not_chosen)

        with tf.GradientTape() as tape:
            scores = model(bx, training=True)
            masked_scores = tf.where(exists, scores, tf.constant(-1e9, tf.float32))

            log_probs = tf.nn.log_softmax(masked_scores, axis=1)
            chosen_log_prob = tf.gather(log_probs, ba, axis=1, batch_dims=1)
            ce_loss = -tf.reduce_mean(chosen_log_prob)

            chosen_score = tf.gather(scores, ba, axis=1, batch_dims=1)
            diff = chosen_score[:, None] - scores
            pair_losses = tf.maximum(0.0, PAIRWISE_MARGIN - diff)
            pair_losses = pair_losses * tf.cast(pair_mask, tf.float32)
            n_pairs = tf.reduce_sum(tf.cast(pair_mask, tf.float32), axis=1)
            pairwise_loss = tf.reduce_mean(
                tf.reduce_sum(pair_losses, axis=1) / tf.maximum(n_pairs, 1.0)
            )

            total_loss = ce_loss + PAIRWISE_LOSS_WEIGHT * pairwise_loss

        grads = tape.gradient(total_loss, model.trainable_variables)
        optimizer.apply_gradients(zip(grads, model.trainable_variables))
        return ce_loss, pairwise_loss

    return train_step


def _make_eval_step(model):
    input_signature = [
        tf.TensorSpec(shape=[None, INPUT_DIM], dtype=tf.float32),
        tf.TensorSpec(shape=[None], dtype=tf.int32),
    ]

    @tf.function(input_signature=input_signature)
    def eval_step(bx, ba):
        exists = _slot_exists_mask(bx)
        action_ids = tf.range(N_ACTIONS, dtype=tf.int32)
        not_chosen = tf.not_equal(action_ids[None, :], ba[:, None])
        pair_mask = tf.logical_and(exists, not_chosen)

        scores = model(bx, training=False)
        masked_scores = tf.where(exists, scores, tf.constant(-1e9, tf.float32))

        log_probs = tf.nn.log_softmax(masked_scores, axis=1)
        chosen_log_prob = tf.gather(log_probs, ba, axis=1, batch_dims=1)
        ce_loss = -tf.reduce_mean(chosen_log_prob)

        chosen_score = tf.gather(scores, ba, axis=1, batch_dims=1)
        diff = chosen_score[:, None] - scores
        pair_losses = tf.maximum(0.0, PAIRWISE_MARGIN - diff)
        pair_losses = pair_losses * tf.cast(pair_mask, tf.float32)
        n_pairs = tf.reduce_sum(tf.cast(pair_mask, tf.float32), axis=1)
        pairwise_loss = tf.reduce_mean(
            tf.reduce_sum(pair_losses, axis=1) / tf.maximum(n_pairs, 1.0)
        )

        pred = tf.argmax(masked_scores, axis=1, output_type=tf.int32)
        top1 = tf.reduce_mean(tf.cast(tf.equal(pred, ba), tf.float32))

        return ce_loss, pairwise_loss, top1

    return eval_step


def evaluate_validation(eval_step, features_mm, actions, val_idx, eval_batch_size=4096):
    ce_sum, pair_sum, top1_sum, n_total = 0.0, 0.0, 0.0, 0
    for start in range(0, len(val_idx), eval_batch_size):
        batch_idx = val_idx[start:start + eval_batch_size]
        bx = np.asarray(features_mm[batch_idx], dtype=np.float32)
        ba = np.asarray(actions[batch_idx], dtype=np.int32)
        ce, pw, top1 = eval_step(tf.convert_to_tensor(bx, tf.float32), tf.convert_to_tensor(ba, tf.int32))
        w = len(batch_idx)
        ce_sum += float(ce) * w
        pair_sum += float(pw) * w
        top1_sum += float(top1) * w
        n_total += w
    return ce_sum / n_total, pair_sum / n_total, top1_sum / n_total


def _split_train_val(n, val_fraction=VAL_FRACTION, seed=123):
    rng = np.random.default_rng(seed)
    perm = rng.permutation(n)
    n_val = max(1, int(round(n * val_fraction)))
    val_idx = np.sort(perm[:n_val])
    train_idx = np.sort(perm[n_val:])
    return train_idx, val_idx


def _make_shuffled_indices(base_indices, rng, block_shuffle_batches=0):
    """base_indices: az elérhető (pl. TRAIN-only) sor-indexek tömbje.
    block_shuffle_batches=0 -> teljesen véletlen (Disk-en lassú)
    block_shuffle_batches=K -> BLOKKOK sorrendjét keveri (K*BatchSize)."""
    n = len(base_indices)
    if block_shuffle_batches <= 0:
        idx = base_indices.copy()
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
        local = base_indices[start:end].copy()
        rng.shuffle(local)
        idx[write_pos:write_pos + len(local)] = local
        write_pos += len(local)
    return idx


def train_ranking_model(features_mm, actions, seed=42, block_shuffle_batches=0,
                         export_path="F:\\MC\\TENSORS"):
    n = features_mm.shape[0]
    train_idx, val_idx = _split_train_val(n, seed=seed + 1)
    print(f"train/val szétválasztás: train={len(train_idx)} sor, val={len(val_idx)} sor "
          f"({len(val_idx) / n:.1%})")

    model = build_policy_model()
    optimizer = tf.keras.optimizers.AdamW(learning_rate=3e-4, weight_decay=1e-5, clipnorm=1.0)
    train_step = _make_train_step(model, optimizer)
    eval_step = _make_eval_step(model)

    rng = np.random.default_rng(seed)
    for epoch in range(EPOCHS):
        startime = Datetime.now()
        indices = _make_shuffled_indices(train_idx, rng, block_shuffle_batches=block_shuffle_batches)
        ce_losses, pair_losses = [], []
        n_train = len(indices)
        for start in range(0, n_train, BATCH_SIZE):
            batchstarttime = Datetime.now()
            batch_idx = indices[start:start + BATCH_SIZE]
            bx = np.asarray(features_mm[batch_idx], dtype=np.float32)
            ba = np.asarray(actions[batch_idx], dtype=np.int32)

            ce_loss, pairwise_loss = train_step(
                tf.convert_to_tensor(bx, tf.float32), tf.convert_to_tensor(ba, tf.int32)
            )
            ce_losses.append(float(ce_loss))
            pair_losses.append(float(pairwise_loss))
            batch_seconds = (Datetime.now() - batchstarttime).total_seconds()
            print(f"epoch={epoch + 1:02d} || batch={start // BATCH_SIZE + 1:04d} / {n_train // BATCH_SIZE + 1:04d} "
                  f"|| CE={ce_loss:.5f} || pairwise={pairwise_loss:.5f} || batch_runtime={batch_seconds:.4f}s/batch", end="\r")
            del bx, ba
            if (start // BATCH_SIZE) % 2500 == 0:
                gc.collect()
                trim_working_set()

        endtime = Datetime.now()
        val_ce, val_pair, val_top1 = evaluate_validation(eval_step, features_mm, actions, val_idx)
        print(f"\nepoch={epoch + 1:02d} || CE={np.mean(ce_losses):.6f} || pairwise={np.mean(pair_losses):.6f} "
              f"|| runtime={(endtime - startime).total_seconds() / 60:.2f} min || runtime={(endtime - startime).total_seconds() / 60} min || remaining={((EPOCHS - epoch - 1) * (endtime - startime)).total_seconds() / 60} min")
        print(f"epoch={epoch + 1:02d} || VALIDÁCIÓ (n={len(val_idx)}) || CE={val_ce:.6f} "
              f"|| pairwise={val_pair:.6f} || top1={val_top1:.1%}")
        diagnose_policy(model, features_mm, actions, idx=train_idx)
        gc.collect()
        trim_working_set()

    model.export(export_path)
    return model


# =============================================================================
# CSV -> memmap (RAM-kímélő, nagy adathoz is)
# =============================================================================

def _count_kept_rows(path, chunk_rows=200_000):
    total, kept = 0, 0
    for chunk in pd.read_csv(path, usecols=EXIST_COLS, chunksize=chunk_rows):
        total += len(chunk)
        kept += int(_keep_mask(chunk).sum())
    return kept, total


def build_memmap_dataset(paths, out_dir="shopping_memmap", chunk_rows=200_000):
    if isinstance(paths, (str, os.PathLike)):
        paths = [paths]
    os.makedirs(out_dir, exist_ok=True)

    print("1/2 -- sorok számlálása...")
    counts = [_count_kept_rows(p, chunk_rows=chunk_rows) for p in paths]
    total_rows = sum(k for k, _ in counts)
    total_raw = sum(t for _, t in counts)
    print(f"     összesen {total_rows} sor marad meg a(z) {total_raw} eredeti sorból "
          f"({total_rows / total_raw:.1%}), {len(paths)} fájlból")

    features_path = os.path.join(out_dir, "features.npy")
    features_mm = np.lib.format.open_memmap(
        features_path, mode="w+", dtype=np.float32, shape=(total_rows, INPUT_DIM)
    )
    actions = np.empty(total_rows, dtype=np.int32)

    print("2/2 -- CSV-k beolvasása és írása a memmap-be...")
    write_pos = 0
    for path in paths:
        first_chunk = True
        for chunk in pd.read_csv(path, chunksize=chunk_rows):
            if first_chunk:
                missing = [c for c in FEATURE_COLS + ["action"] if c not in chunk.columns]
                if missing:
                    raise ValueError(f"Missing CSV columns in {path}: {missing}")
                first_chunk = False

            keep = _keep_mask(chunk).to_numpy()
            chunk = chunk[keep]
            n = len(chunk)
            if n == 0:
                continue

            chunk_features = chunk[FEATURE_COLS].to_numpy(np.float32)
            chunk_features = _normalize_context_features(chunk_features)
            features_mm[write_pos:write_pos + n] = chunk_features
            actions[write_pos:write_pos + n] = chunk["action"].to_numpy(np.int32)
            write_pos += n
            print(f"     ...{write_pos}/{total_rows} sor feldolgozva", end="\r")

    features_mm.flush()
    print(f"\n     kész: {write_pos} sor a memmap-ben ({features_path})")
    assert write_pos == total_rows, (
        f"Eltérés a számolt ({total_rows}) és a ténylegesen írt ({write_pos}) sorok között!")

    np.save(os.path.join(out_dir, "actions.npy"), actions)
    print(f"     akciók elmentve: {out_dir}/actions.npy")
    return out_dir


def load_memmap_dataset(mm_dir="shopping_memmap"):
    features_mm = np.load(os.path.join(mm_dir, "features.npy"), mmap_mode="r")
    actions = np.load(os.path.join(mm_dir, "actions.npy"))
    return features_mm, actions


if __name__ == "__main__":
    CSV_PATHS = [
        "data\\shopping_experience_1.csv",
        "data\\shopping_experience_2.csv",
        "data\\shopping_experience_3.csv",
        "data\\shopping_experience_4.csv",
    ]
    K_block_shuffle_batches = 32

    MM_DIR = "shopping_memmap"
    if not os.path.exists(os.path.join(MM_DIR, "features.npy")):
        build_memmap_dataset(CSV_PATHS, out_dir=MM_DIR)
    else:
        print(f"Már létezik memmap itt: {MM_DIR} -- ha új CSV-kből "
              f"szeretnéd újraépíteni, töröld ezt a mappát.")

    features_mm, actions = load_memmap_dataset(MM_DIR)
    print(f"rows={features_mm.shape[0]} input_dim={features_mm.shape[1]}")

    model = train_ranking_model(features_mm, actions, block_shuffle_batches=K_block_shuffle_batches)

    order, scores, probs = ranking_from_model(model, np.asarray(features_mm[0]))
    diagnose_policy(model, features_mm, actions)

    print("ranking:", order)
    print("scores:", np.round(scores, 3))
    print("probs:", np.round(probs, 3))
