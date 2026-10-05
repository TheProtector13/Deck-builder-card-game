import os
import math
import numpy as np
import pandas as pd
import tensorflow as tf
from tensorflow.keras import layers, models

# -----------------------------------------------------------------------------
# Shopping NN v2
# Input:
#   5  StrategyAI output
#   5  own fraction distribution / synergy context
#   6 x 15 card features:
#      exists, price_ratio_to_money, free_effect, 12 effect one-hot
# Total = 5 + 5 + 90 = 100
# Output = 6 action values (Q-like scores).
# Inference: softmax(scores / temperature) only for a percentage-like ranking.
# -----------------------------------------------------------------------------

INPUT_DIM = 100
N_ACTIONS = 6
GAMMA = 0.98
BATCH_SIZE = 512
EPOCHS = 20
TEMPERATURE = 0.20

META_COLS = [
    "episode", "decision", "actor", "money", "my_health", "enemy_health"
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
    )
]
TARGET_COLS = ["action", "reward", "done"]

assert len(FEATURE_COLS) == INPUT_DIM, len(FEATURE_COLS)


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

    x = df[FEATURE_COLS].to_numpy(np.float32)
    actions = df["action"].to_numpy(np.int32)
    rewards = df["reward"].to_numpy(np.float32)
    done = df["done"].to_numpy(np.float32)

    next_x = np.zeros_like(x)
    has_next = np.zeros(len(df), dtype=np.float32)
    groups = df.groupby(["episode", "actor"], sort=False).indices
    for _, indices in groups.items():
        idx = np.asarray(indices, dtype=np.int64)
        if len(idx) > 1:
            next_x[idx[:-1]] = x[idx[1:]]
            has_next[idx[:-1]] = 1.0

    return df, x, actions, rewards, done, next_x, has_next


def build_model() -> tf.keras.Model:
    inputs = layers.Input(shape=(INPUT_DIM,))
    x = layers.Dense(256, activation="silu")(inputs)
    x = layers.LayerNormalization()(x)
    x = layers.Dense(256, activation="silu")(x)
    x = layers.Dropout(0.10)(x)
    x = layers.Dense(128, activation="silu")(x)
    x = layers.Dense(64, activation="silu")(x)
    outputs = layers.Dense(N_ACTIONS, activation=None)(x)
    return models.Model(inputs, outputs)


def train_q_model(df, x, actions, rewards, done, next_x, has_next):
    model = build_model()
    target_model = build_model()
    target_model.set_weights(model.get_weights())

    optimizer = tf.keras.optimizers.AdamW(
        learning_rate=3e-4,
        weight_decay=1e-5,
        clipnorm=1.0,
    )
    huber = tf.keras.losses.Huber(reduction="none")

    n = len(x)
    if n < 2:
        raise ValueError("Not enough shopping experience rows.")

    indices = np.arange(n)
    rng = np.random.default_rng(42)

    for epoch in range(EPOCHS):
        rng.shuffle(indices)
        losses = []

        for start in range(0, n, BATCH_SIZE):
            batch_idx = indices[start:start + BATCH_SIZE]
            bx = tf.convert_to_tensor(x[batch_idx], tf.float32)
            ba = tf.convert_to_tensor(actions[batch_idx], tf.int32)
            br = tf.convert_to_tensor(rewards[batch_idx], tf.float32)
            bd = tf.convert_to_tensor(done[batch_idx], tf.float32)
            bnext = tf.convert_to_tensor(next_x[batch_idx], tf.float32)
            bhas_next = tf.convert_to_tensor(has_next[batch_idx], tf.float32)

            next_q = tf.reduce_max(target_model(bnext, training=False), axis=1)
            target = br + GAMMA * next_q * (1.0 - bd) * bhas_next

            with tf.GradientTape() as tape:
                q_values = model(bx, training=True)
                chosen_q = tf.gather(q_values, ba, axis=1, batch_dims=1)
                loss = tf.reduce_mean(huber(target, chosen_q))

            grads = tape.gradient(loss, model.trainable_variables)
            optimizer.apply_gradients(zip(grads, model.trainable_variables))
            losses.append(float(loss))

        target_model.set_weights(model.get_weights())
        print(f"epoch={epoch + 1:02d} loss={np.mean(losses):.6f}")

    model.export("F:\\MC\\TENSORS")
    return model


SLOT_FEATURE_DIM = 15
SLOT_BLOCK_START = 10  # 5 strategy + 5 synergy


def augment_slot_permutations(x, actions, rewards, done, next_x, has_next, n_permutations=2, seed=0):
    rng = np.random.default_rng(seed)
    n = len(x)
    slots = x[:, SLOT_BLOCK_START:].reshape(n, 6, SLOT_FEATURE_DIM)

    x_parts = [x]
    action_parts = [actions]
    reward_parts = [rewards]
    done_parts = [done]
    next_x_parts = [next_x]
    has_next_parts = [has_next]

    for _ in range(n_permutations):
        perms = np.array([rng.permutation(6) for _ in range(n)])  # (n, 6)
        permuted_slots = np.take_along_axis(slots, perms[:, :, None], axis=1)

        new_x = x.copy()
        new_x[:, SLOT_BLOCK_START:] = permuted_slots.reshape(n, -1)
        new_actions = np.argmax(perms == actions[:, None], axis=1).astype(np.int32)

        x_parts.append(new_x)
        action_parts.append(new_actions)
        reward_parts.append(rewards)
        done_parts.append(done)
        next_x_parts.append(next_x)
        has_next_parts.append(has_next)

    return (
        np.concatenate(x_parts, axis=0),
        np.concatenate(action_parts, axis=0),
        np.concatenate(reward_parts, axis=0),
        np.concatenate(done_parts, axis=0),
        np.concatenate(next_x_parts, axis=0),
        np.concatenate(has_next_parts, axis=0),
    )


def masked_probabilities(model, features: np.ndarray, temperature: float = TEMPERATURE):
    x = np.asarray(features, dtype=np.float32).reshape(1, -1)
    scores = model.predict(x, verbose=0)[0]

    exists = np.array([
        features[10 + slot * 15]  # 5 strategy + 5 synergy + slot0 exists
        for slot in range(6)
    ], dtype=np.float32)

    masked = np.where(exists > 0.5, scores, -1e9)
    logits = masked / max(temperature, 1e-6)
    logits -= np.max(logits)
    probs = np.exp(logits)
    probs = probs / np.sum(probs)
    return scores, probs


def ranking_from_model(model, features: np.ndarray):
    scores, probs = masked_probabilities(model, features)
    order = np.argsort(probs)[::-1]
    return order.tolist(), scores.tolist(), probs.tolist()

if __name__ == "__main__":
    CSV_PATHS = [
        "shopping_experience_randomai.csv",
        "shopping_experience_heuristic.csv",
    ]
    AUGMENT_PERMUTATIONS = 2  # 0 = kikapcsolva

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
    print("ranking:", order)
    print("scores:", np.round(scores, 3))
    print("probs:", np.round(probs, 3))
