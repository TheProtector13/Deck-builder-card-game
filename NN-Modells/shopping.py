import os 
import numpy as np 
import math 
from itertools import product 
import tensorflow as tf 
from tensorflow.keras import layers, models

X_path = 'X_memmap.dat' 
Y_path = 'Y_memmap.dat' 
META_path = 'memmap_meta.npz' # tárolja a tényleges mintaszámot 
D = 89 # bemenet dim (5 pref + 6*14) 
C = 6 # kimenet dim 
mapping = [-1, 1, 5, 4, 6, 3]
rng = np.random.default_rng()

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

def slot_score(slot, preferred_Attribute):
    # preferred_Attribute: -1 = nincs preferencia, 0-11 = preferált képesség indexe
    if slot[0] == 0:
        return -1  # nincs kártya

    abilities = np.where(slot[1:] == 1)[0]
    if len(abilities) == 0:
        return 0  # van kártya, de nincs képesség

    if preferred_Attribute != -1:
        if preferred_Attribute in abilities:
            return 13  # preferált képesség van → max pont + 1
    # minél balrább, annál fontosabb
    return 12 - abilities[0]


def compute_output(slots, hp_flags, preferred=-1):
    scores = [slot_score(slot, preferred) for slot in slots]
    max_score = max(scores)
    if max_score < 0:
        return None  # minden slot üres → kihagyjuk
        
    y = np.zeros(6, dtype=np.float32)  
    winners = [i for i, s in enumerate(scores) if s == max_score]
    if len(winners) == 1:
        y[winners[0]] = 1.0
        return y
    
    # highprice winner szűrés
    hp_winners = [i for i in winners if hp_flags[i] == 1]
    if len(hp_winners) >= 1:
        chosen = hp_winners[0]
    else:
        chosen = winners[0]
    
    y[chosen] = 1.0
    return y
    
def sample_highprice_flags_for_slots(slots13, rng):
    exists_idx = [i for i, s in enumerate(slots13) if s[0] == 1]
    flags = np.zeros(6, dtype=np.float32)
    if len(exists_idx) == 0:
        return flags
    max_k = min(3, len(exists_idx))
    k = rng.integers(1, max_k + 1)
    chosen = rng.choice(exists_idx, size=k, replace=False)
    flags[chosen] = 1.0
    return flags

X_e = [
[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
 0, 0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
 0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0],
[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
 0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0],
[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0],
[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0],
[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0],
[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0],
[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0],
 [0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 
 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0]]

Y_e = [
    [0, 0, 0, 0, 0, 1],
    [0, 0, 0, 0, 0, 1],
    [0, 0, 0, 0, 0, 1],
    [0, 0, 0, 0, 0, 1],
    [0, 0, 0, 0, 0, 1],
    [0, 0, 0, 0, 0, 1],
    [0, 0, 0, 0, 0, 1],
    [0, 0, 0, 0, 0, 1],
    [0, 0, 0, 0, 0, 1],
    [0, 0, 0, 0, 0, 1],
    [0, 0, 0, 0, 0, 1]
]

slot_states = generate_slot_states()

# Ha memmap és meta létezik, átugorjuk a generálást
if os.path.exists(X_path) and os.path.exists(Y_path) and os.path.exists(META_path):
    meta = np.load(META_path)
    N_samples = int(meta['n_samples'])
    print(f"Memmap fájlok megtalálva. Mintaszám: {N_samples}. Generálás átugorva.")
else:
    # generálás
    max_possible = (len(slot_states) ** 6) * len(mapping)
    print(f"Max lehetséges minta: {max_possible}. Kezdődik a generálás és írás memmapba...")

    X_mm = np.memmap(X_path, dtype='float16', mode='w+', shape=(max_possible, D))
    Y_mm = np.memmap(Y_path, dtype='float16', mode='w+', shape=(max_possible, C))

    idx = 0
    try:
        # iterálunk a teljes tér felett; csak az érvényes mintákat írjuk
        for slots in product(slot_states, repeat=6):
            hp_flags = sample_highprice_flags_for_slots(slots, rng)
            new_slots = []
            for i, s in enumerate(slots):
                new_s = np.insert(s, 1, hp_flags[i]).astype(np.float32)
                new_slots.append(new_s)
                
            slots_concat = np.concatenate(new_slots)
            for pref_idx in range(len(mapping)):
                ability_val = mapping[pref_idx]
                preferred = (ability_val - 1) if ability_val != -1 else -1
                y = compute_output(slots, hp_flags, preferred=preferred)
                if y is None:
                    continue
                prefX = np.zeros(5, dtype=np.float32)
                if pref_idx != 0:
                    prefX[pref_idx - 1] = 1.0
                x = np.concatenate((prefX, slots_concat)).astype(np.float32)  # 83 float32
                # tárolás float16-ban
                X_mm[idx, :] = x.astype(np.float16)
                Y_mm[idx, :] = y.astype(np.float16)
                idx += 1
                # státusz
                if idx % 1_000_000 == 0:
                    print(f"Írva: {idx} minta...")
        N_samples = idx
        print(f"Generálás kész. Összes érvényes minta: {N_samples}")
    except KeyboardInterrupt:
        N_samples = idx
        print(f"Generálás megszakítva. Eddig írt minták: {N_samples}")
    finally:
        # flush és felszabadítás
        X_mm.flush()
        Y_mm.flush()
        del X_mm, Y_mm
        np.savez_compressed(META_path, n_samples=N_samples)

# ---------- Betöltés memmapból és tf.data készítése ----------
def memmap_dataset(X_path, Y_path, n_samples, batch_size=65536, shuffle_buffer=10):
    D = 89; C = 6
    X_mm = np.memmap(X_path, dtype='float16', mode='r', shape=(n_samples, D))
    Y_mm = np.memmap(Y_path, dtype='float16', mode='r', shape=(n_samples, C))
    def gen():
        for start in range(0, n_samples, batch_size):
            end = min(start + batch_size, n_samples)
            xb = X_mm[start:end].astype(np.float32) 
            yb = Y_mm[start:end].astype(np.float32) 
            yield xb, yb
    ds = tf.data.Dataset.from_generator(
        lambda: gen(),
        output_types=(tf.float32, tf.float32),
        output_shapes=( tf.TensorShape([None, D]), tf.TensorShape([None, C]) )
    )
    ds = ds.shuffle(shuffle_buffer)
    ds = ds.repeat()
    ds = ds.prefetch(tf.data.AUTOTUNE)
    return ds

# ---------- Eval data betöltése ----------
def load_eval_batch(X_path, Y_path, start, size, n_samples, D=89, C=6):
    X_mm = np.memmap(X_path, dtype='float16', mode='r', shape=(n_samples, D))
    Y_mm = np.memmap(Y_path, dtype='float16', mode='r', shape=(n_samples, C))
    end = min(start + size, n_samples)
    xb = X_mm[start:end].astype(np.float32)
    yb = Y_mm[start:end].astype(np.float32)
    return xb, yb

# betöltjük a meta fájlt, ha nem volt korábban
meta = np.load(META_path)
N_samples = int(meta['n_samples'])
print(f"Betöltés memmapból: {N_samples} minta.")

train_ds = memmap_dataset(X_path, Y_path, N_samples, batch_size=32768, shuffle_buffer=200)

leaky_model = models.Sequential(
    [
        layers.Input(shape=(89,)),
        layers.Dense(128, activation="silu"),
        layers.Dense(64, activation="silu"),
        layers.Dense(6, activation="softmax"),
    ]
)

opt = tf.keras.optimizers.AdamW(learning_rate=15e-5, weight_decay=1e-4)
leaky_model.compile(
    optimizer=opt,
    loss="categorical_crossentropy",
    metrics=["accuracy"],
)

# steps_per_epoch beállítása
steps_per_epoch = math.ceil(N_samples / 32768)
print(f"Steps per epoch: {steps_per_epoch}")

# fit
leaky_model.fit(
    train_ds,
    epochs=200, 
    steps_per_epoch=steps_per_epoch,
    verbose=1)

X_eval = np.array(X_e, dtype=np.float32)
Y_eval = np.array(Y_e, dtype=np.float32)

loss, acc = leaky_model.evaluate(X_eval, Y_eval)
print("Test leaky loss:", loss, "Test acc:", acc)

xb, yb = load_eval_batch(X_path, Y_path, start=20000000, size=655360, n_samples=N_samples)
loss, acc = leaky_model.evaluate(xb, yb, batch_size=xb.shape[0], verbose=1)
print("Test leaky loss:", loss, "Test acc:", acc)

#leaky_model.export("F:\\MC\\TENSORS")
