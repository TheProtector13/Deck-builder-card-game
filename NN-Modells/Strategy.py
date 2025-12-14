import tensorflow as tf
from tensorflow.keras import layers, models

model = models.Sequential(
    [
        layers.Input(shape=(5,)),
        layers.Dense(32, activation="relu"),
        layers.Dense(16, activation="relu"),
        layers.Dense(
            5, activation="softmax"
        ),  # vagy activation='sigmoid' ha független outputokat akarsz
    ]
)

model.compile(
    optimizer="adam",
    loss="categorical_crossentropy",  # ha softmax + arányokra tanítasz
    metrics=["accuracy"],  # vagy egyéni mérőszám (pl. MSE, vagy custom loss)
)

leaky_model = models.Sequential(
    [
        layers.Input(shape=(5,)),
        layers.Dense(32, activation="leaky_relu"),
        layers.Dense(16, activation="leaky_relu"),
        layers.Dense(
            5, activation="softmax"
        ),  # vagy activation='sigmoid' ha független outputokat akarsz
    ]
)

leaky_model.compile(
    optimizer="adam",
    loss="categorical_crossentropy",  # ha softmax + arányokra tanítasz
    metrics=["accuracy"],  # vagy egyéni mérőszám (pl. MSE, vagy custom loss)
)

# Feltételezve, hogy van X_train, y_train és X2_train, y2_train

history1 = model.fit(
    X_train, y_train, batch_size=32, epochs=50, validation_split=0.2, shuffle=True
)

history2 = leaky_model.fit(
    X2_train, y2_train, batch_size=32, epochs=50, validation_split=0.2, shuffle=True
)

loss, acc = model.evaluate(X_train, y_train)
print("Test loss:", loss, "Test acc:", acc)

loss, acc = leaky_model.evaluate(X_train, y_train)
print("Test leaky loss:", loss, "Test acc:", acc)
