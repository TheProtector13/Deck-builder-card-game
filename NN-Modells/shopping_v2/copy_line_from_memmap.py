import numpy as np

FEATURE_COLS = (
    [f"strategy_{i}" for i in range(5)]
    + [f"synergy_fraction_{i}" for i in range(5)]
    + [
        f"slot{s}_{name}"
        for s in range(6)
        for name in (
            ["exists", "price_ratio", "free_effect"]
            + [f"effect_{e}" for e in range(12)]
            + ["attack", "health", "money"]
            + [f"fraction_{f}" for f in range(5)]
            + ["synergy"]
        )
    ]
    + ["my_health", "enemy_health", "my_deck_value", "enemy_deck_value"]
)

# Memmap megnyitása olvasásra
features_mm = np.load("shopping_memmap/features.npy", mmap_mode="r")

# Egy véletlenszerű sor kiválasztása
row_idx = np.random.randint(0, len(features_mm))
sample_row = features_mm[row_idx]

print(f"=== Memmap kiválasztott sora (Index: {row_idx}) ===")
for i, (name, val) in enumerate(zip(FEATURE_COLS, sample_row), 1):
    print(f"{i:3d}. {name:25s} = {val:.4f}")

print("\n=== Másolható formátum ===")
copyable_string = ", ".join(f"{val:.4f}" for val in sample_row)
print(copyable_string)
