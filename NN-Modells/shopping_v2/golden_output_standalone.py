import numpy as np
import tensorflow as tf

# ===================== Ellenőrzendő háló adatai ======================
MODEL_PATH = r"E:\documents\tanulás\Online Oktatás\Fősuli\MGR\MGR_munka\TEST\CardGame\NN\shoppingAI"

GOLDEN_INPUT = [
    0.1521, 0.2852, 0.0535, 0.4222, 0.0869, 0.8333, 0.0000, 0.1667, 0.0000, 0.0000, 1.0000, 0.2000, 1.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 1.0000, 0.0000, 0.0000, 0.4000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.1430, 1.0000, 0.5000, 1.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 1.0000, 0.0000, 0.0000, 0.0000, 0.1000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 1.0000, 0.0000, 1.0000, 0.3000, 1.0000, 0.0000, 0.0000, 1.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.2000, 0.0000, 0.0000, 0.0000, 0.0000, 1.0000, 0.0000, 1.0000, 0.4000, 1.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 1.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.1667, 0.0000, 0.0000, 0.0000, 1.0000, 0.0000, 0.0000, 0.0000, 0.0000, 1.0000, 0.4000, 0.0000, 0.0000, 0.0000, 0.0000, 1.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.1111, 0.1000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 1.0000, 0.0000, 1.0000, 0.3000, 1.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 1.0000, 0.0000, 0.0556, 0.0000, 0.2000, 0.0000, 0.0000, 0.0000, 0.0000, 1.0000, 0.0000, 1.1333, 0.5778, 1.0612, 0.8623
]
# =====================================================================


def main():
    x = np.asarray(GOLDEN_INPUT, dtype=np.float32).reshape(1, -1)
    if x.shape[1] != 158:
        raise ValueError(
            f"A GOLDEN_INPUT hossza {x.shape[1]}, 158-nak kellene lennie -- "
            f"biztosan bemásoltad mind a 158 értéket?"
        )

    print(f"Betöltés innen: {MODEL_PATH}")
    loaded = tf.saved_model.load(MODEL_PATH)
    infer = loaded.signatures["serving_default"]

    input_key = list(infer.structured_input_signature[1].keys())[0]
    print(f"(bemeneti tensor neve a gráfban: {input_key!r})")

    out = infer(**{input_key: tf.convert_to_tensor(x)})
    output_key = list(out.keys())[0]
    scores = out[output_key].numpy()[0]

    print(f"(kimeneti tensor neve a gráfban: {output_key!r})")
    print()
    print("Kimeneti pontszámok:")
    for i, s in enumerate(scores):
        print(f"  score[{i}] = {s:.8f}")
    print()
    print("Ezt vesd össze a C# `golden_output_csharp.cs` teszt kimenetével,")
    print("és/vagy egy másik MODEL_PATH-tal futtatott eredménnyel.")


if __name__ == "__main__":
    main()
