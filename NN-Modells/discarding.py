import tensorflow as tf
from tensorflow.keras import layers, models
import numpy as np
from itertools import product


def generate_slot_states():
    states = []

    # üres slot
    states.append(np.zeros(13, dtype=np.float32))

    # létező kártya, képesség nélkül
    base = np.zeros(13, dtype=np.float32)
    base[0] = 1
    states.append(base)

    # létező kártya, pontosan 1 képességgel
    for ability in range(1, 13):
        v = np.zeros(13, dtype=np.float32)
        v[0] = 1
        v[ability] = 1
        states.append(v)

    return states


def slot_score(slot):
    if slot[0] == 0:
        return -1  # nincs kártya

    abilities = np.where(slot[1:] == 1)[0]
    if len(abilities) == 0:
        return 0  # van kártya, de nincs képesség

    # minél balrább, annál fontosabb
    return 12 - abilities[0]


def compute_output(slots):
    scores = [slot_score(slot) for slot in slots]
    valid_scores = [(i, s) for i, s in enumerate(scores) if s >= 0]

    if not valid_scores:
        return np.zeros(2, dtype=np.float32)

    min_score = min(s for _, s in valid_scores)

    y = np.zeros(2, dtype=np.float32)

    winners = [i for i, s in valid_scores if s == min_score]
    value = 1.0 / len(winners)

    for i in winners:
        y[i] = value

    return y


X_e = [
    [1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    [1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0],
    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
]

Y_e = [
    [0.5, 0.5],
    [1, 0],
    [0, 1],
    [0, 1],
    [0, 1],
    [0, 1],
    [0, 1],
    [0, 1],
    [0, 1],
    [0.5, 0.5],
]

x_t = []
y_t = []

slot_states = generate_slot_states()

for slots in product(slot_states, repeat=2):
    y = compute_output(slots)
    x = np.concatenate(slots)
    x_t.append(x)
    y_t.append(y)

X_train = np.array(x_t, dtype=np.float32)
y_train = np.array(y_t, dtype=np.float32)

idx = np.random.permutation(len(X_train))
X_train = X_train[idx]
y_train = y_train[idx]

X_eval = np.array(X_e, dtype=np.float32)
Y_eval = np.array(Y_e, dtype=np.float32)

print(X_train.shape)

leaky_model = models.Sequential(
    [
        layers.Input(shape=(26,)),
        layers.Dense(78, activation="silu"),
        layers.Dropout(0.3),
        layers.Dense(26, activation="silu"),
        layers.Dense(2, activation="softmax"),
    ]
)

leaky_model.compile(
    optimizer="adam",
    loss="categorical_crossentropy",
    metrics=["accuracy"],
)

history2 = leaky_model.fit(
    X_train,
    y_train,
    batch_size=4096,
    epochs=1000,
    validation_split=0.05,
    shuffle=True,
    verbose=1,
)


loss, acc = leaky_model.evaluate(X_eval, Y_eval)
print("Test leaky loss:", loss, "Test acc:", acc)

for i in range(len(X_eval)):
    x = X_eval[i]
    y_true = Y_eval[i]

    y_pred = leaky_model.predict(x.reshape(1, -1), verbose=0)[0]
    y_pred = np.round(y_pred, 2)

    print(
        "Input:", x,
        "Predicted:", y_pred,
        "True:", y_true
    )

#leaky_model.export("D:\\MC\\TENSORS")
