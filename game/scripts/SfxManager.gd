extends Node

const SAMPLE_RATE := 22050
const POOL_SIZE := 12

var muted: bool = false
var _players: Array[AudioStreamPlayer] = []
var _pool_idx: int = 0
var _streams: Dictionary = {}


func _ready() -> void:
	for i in range(POOL_SIZE):
		var p := AudioStreamPlayer.new()
		p.volume_db = -8.0
		add_child(p)
		_players.append(p)

	_streams["screw_tick"] = _make_tone(620.0, 480.0, 0.045, "square", 0.25)
	_streams["screw_pop"] = _make_tone(520.0, 980.0, 0.085, "sine", 0.45)
	_streams["scrap_open"] = _make_burst(240.0, 620.0, 0.16, 0.55)
	_streams["collect"] = _make_tone(660.0, 880.0, 0.09, "sine", 0.40)
	_streams["crush"] = _make_crush_thud(0.28)
	_streams["smelt"] = _make_tone(880.0, 1320.0, 0.16, "triangle", 0.42)
	_streams["buy"] = _make_chord([523.25, 659.25, 783.99], 0.18, 0.38)
	_streams["fever"] = _make_chord([440.0, 554.37, 659.25, 880.0], 0.32, 0.45)
	_streams["boss"] = _make_tone(220.0, 140.0, 0.35, "saw", 0.42)
	_streams["magnet"] = _make_tone(320.0, 740.0, 0.18, "sine", 0.32)
	_streams["laser"] = _make_tone(1400.0, 420.0, 0.14, "saw", 0.28)


func _play(key: String, pitch: float = 1.0, vol_offset: float = 0.0) -> void:
	if muted or not _streams.has(key):
		return
	var p: AudioStreamPlayer = _players[_pool_idx]
	_pool_idx = (_pool_idx + 1) % POOL_SIZE
	p.stream = _streams[key]
	p.pitch_scale = clampf(pitch, 0.5, 2.2)
	p.volume_db = -8.0 + vol_offset
	p.play()


func play_screw_tick(progress: float = 0.0) -> void:
	_play("screw_tick", 0.9 + progress * 0.45, -4.0)


func play_screw_pop() -> void:
	_play("screw_pop", randf_range(0.95, 1.15), -1.0)


func play_scrap_open() -> void:
	_play("scrap_open", randf_range(0.95, 1.08), 1.0)


func play_collect(combo: int = 0) -> void:
	var scale_steps := [1.0, 1.122, 1.26, 1.498, 1.682, 2.0]
	var pitch: float = scale_steps[mini(combo, scale_steps.size() - 1)]
	_play("collect", pitch, -2.0)


func play_crush() -> void:
	_play("crush", randf_range(0.92, 1.05), 3.0)


func play_smelt() -> void:
	_play("smelt", randf_range(0.96, 1.12), -1.0)


func play_buy() -> void:
	_play("buy", 1.0, 0.0)


func play_fever() -> void:
	_play("fever", 1.0, 2.0)


func play_boss() -> void:
	_play("boss", 1.0, 2.0)


func play_magnet() -> void:
	_play("magnet", 1.0, -3.0)


func play_laser() -> void:
	_play("laser", randf_range(0.95, 1.15), -3.0)


func _make_tone(f0: float, f1: float, dur: float, wave: String, amp: float) -> AudioStreamWAV:
	var count := int(SAMPLE_RATE * dur)
	var bytes := PackedByteArray()
	bytes.resize(count * 2)
	var phase := 0.0
	for i in range(count):
		var u := float(i) / float(maxi(count - 1, 1))
		var freq := lerpf(f0, f1, u)
		phase += TAU * freq / float(SAMPLE_RATE)
		var s := 0.0
		match wave:
			"sine":
				s = sin(phase)
			"square":
				s = 1.0 if sin(phase) >= 0.0 else -1.0
			"triangle":
				s = 2.0 * absf(2.0 * (phase / TAU - floorf(phase / TAU + 0.5))) - 1.0
			"saw":
				s = 2.0 * (phase / TAU - floorf(phase / TAU + 0.5))
		var env := minf(u * 18.0, 1.0) * pow(1.0 - u, 1.6)
		bytes.encode_s16(i * 2, clampi(int(s * env * amp * 32767.0), -32767, 32767))
	return _wrap_wav(bytes)


func _make_burst(f0: float, f1: float, dur: float, amp: float) -> AudioStreamWAV:
	var count := int(SAMPLE_RATE * dur)
	var bytes := PackedByteArray()
	bytes.resize(count * 2)
	var phase := 0.0
	for i in range(count):
		var u := float(i) / float(maxi(count - 1, 1))
		var freq := lerpf(f0, f1, u)
		phase += TAU * freq / float(SAMPLE_RATE)
		var noise := randf_range(-1.0, 1.0) * (1.0 - u) * 0.45
		var s := sin(phase) * 0.7 + noise
		var env := minf(u * 25.0, 1.0) * pow(1.0 - u, 1.8)
		bytes.encode_s16(i * 2, clampi(int(s * env * amp * 32767.0), -32767, 32767))
	return _wrap_wav(bytes)


func _make_crush_thud(dur: float) -> AudioStreamWAV:
	var count := int(SAMPLE_RATE * dur)
	var bytes := PackedByteArray()
	bytes.resize(count * 2)
	var phase := 0.0
	for i in range(count):
		var u := float(i) / float(maxi(count - 1, 1))
		var freq := lerpf(150.0, 38.0, pow(u, 0.5))
		phase += TAU * freq / float(SAMPLE_RATE)
		var thump := sin(phase) * pow(1.0 - u, 1.5)
		var crunch := randf_range(-1.0, 1.0) * pow(1.0 - u, 2.4) * 0.65
		var s := clampf(thump * 0.85 + crunch, -1.0, 1.0)
		var env := minf(u * 60.0, 1.0)
		bytes.encode_s16(i * 2, clampi(int(s * env * 0.75 * 32767.0), -32767, 32767))
	return _wrap_wav(bytes)


func _make_chord(freqs: Array, dur: float, amp: float) -> AudioStreamWAV:
	var count := int(SAMPLE_RATE * dur)
	var bytes := PackedByteArray()
	bytes.resize(count * 2)
	for i in range(count):
		var u := float(i) / float(maxi(count - 1, 1))
		var t := float(i) / float(SAMPLE_RATE)
		var s := 0.0
		for f in freqs:
			s += sin(TAU * float(f) * t)
		s /= float(maxi(freqs.size(), 1))
		var env := minf(u * 20.0, 1.0) * pow(1.0 - u, 1.4)
		bytes.encode_s16(i * 2, clampi(int(s * env * amp * 32767.0), -32767, 32767))
	return _wrap_wav(bytes)


func _wrap_wav(bytes: PackedByteArray) -> AudioStreamWAV:
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = SAMPLE_RATE
	wav.stereo = false
	wav.data = bytes
	return wav
