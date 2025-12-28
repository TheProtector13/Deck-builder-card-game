import os
import tensorflow as tf
from tensorflow.keras import layers, models
import numpy as np
from itertools import combinations

# --- definíciók ---
x_def = np.eye(5, dtype=np.float32)

y_def = np.array(
    [
        [0, 0.2, 0.5, 0, 0.3],
        [0.1, 0, 0.3, 0.2, 0.4],
        [0.2, 0.3, 0, 0.4, 0.1],
        [0.5, 0.2, 0, 0, 0.3],
        [0.3, 0, 0.5, 0.2, 0],
    ],
    dtype=np.float32,
)

# --- tanító adatok gyűjtése ---
x_t = [[0, 0, 0, 0, 0]]
y_t = [[0.2, 0.2, 0.2, 0.2, 0.2]]

# egységvektorok
for i in range(5):
    x_t.append(x_def[i].tolist())
    y_t.append(y_def[i].tolist())


# --- súlykombinációk generálása ---
def weight_sets(k, step=0.1):
    n = int(1 / step)
    if k == 1:
        return [[1.0]]

    result = []

    def gen(curr, left, depth):
        if depth == k - 1:
            curr.append(left)
            result.append(curr.copy())
            curr.pop()
            return
        for i in range(1, left):
            curr.append(i)
            gen(curr, left - i, depth + 1)
            curr.pop()

    gen([], n, 0)
    return [np.array(w, dtype=np.float32) * step for w in result]


# --- kombinációk kiszámolása ---
for k in range(2, 6):  # 2..5
    weights_list = weight_sets(k, step=0.1)

    for combo in combinations(range(5), k):
        for weights in weights_list:
            # x vektor
            x = np.zeros(5, dtype=np.float32)
            for idx, w in zip(combo, weights):
                x[idx] = w

            # y vektor
            y = np.zeros(5, dtype=np.float32)
            for idx, w in zip(combo, weights):
                y += w * y_def[idx]

            # hozzáadás
            x_t.append(x.tolist())
            y_t.append(y.tolist())

# --- numpy tömbbé alakítás ---
X_train = np.array(x_t, dtype=np.float32)
y_train = np.array(y_t, dtype=np.float32)

X_eval = np.array([
    [0, 1, 0, 0, 0],
    [0.5, 0.5, 0, 0, 0],
    [0.5, 0, 0.5, 0, 0]
    ], dtype=np.float32)
Y_eval = np.array([
    [0.1, 0, 0.3, 0.2, 0.4],
    [0.05, 0.1, 0.4, 0.1, 0.35],
    [0.1, 0.25, 0.25, 0.2, 0.2]
    ], dtype=np.float32)

# --- ellenőrzés ---
print("X_train shape:", X_train.shape)
print("y_train shape:", y_train.shape)
print("példa sor:")
print(X_train[10], "=>", y_train[10])

leaky_model = models.Sequential(
    [
        layers.Input(shape=(5,)),
        #layers.Dense(15, activation="leaky_relu"),
        #layers.Dense(8, activation="leaky_relu"),
        layers.Dense(
            5, activation="softmax"
        ),
    ]
)

leaky_model.compile(
    optimizer="adam",
    loss="categorical_crossentropy",
    metrics=["accuracy"],
)

history2 = leaky_model.fit(
    X_train, y_train, batch_size=32, epochs=500, validation_split=0.1, shuffle=True
)


loss, acc = leaky_model.evaluate(X_eval, Y_eval)
print("Test leaky loss:", loss, "Test acc:", acc)

leaky_model.export("D:\\MC\\TENSORS")
