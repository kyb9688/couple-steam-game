extends Node

signal state_changed
signal skill_purchased(skill_id: String, new_level: int)
signal fever_started
signal boss_defeated(tier: int)
signal ending_unlocked

const SAVE_PATH := "user://scrap_to_gold_save.json"

var ui_font: SystemFont
var parts_by_id: Dictionary = {}
var scraps_list: Array = []
var skills_list: Array = []
var skills_by_id: Dictionary = {}

# Runtime state
var money: float = 0.0
var total_earned: float = 0.0
var unlocked_tier: int = 1
var current_tier: int = 1
var bosses_defeated: Array = []
var skill_levels: Dictionary = {}

var fever_charge: float = 0.0
var fever_time_left: float = 0.0

var total_screws_removed: int = 0
var total_scraps_dismantled: int = 0
var total_ingots_smelted: int = 0
var play_time_sec: float = 0.0
var ending_reached: bool = false

# Rolling money/sec tracker (last 5 seconds)
var _recent_earnings: Array = []
var money_per_sec: float = 0.0
var _autosave_timer: float = 0.0


func _ready() -> void:
	ui_font = SystemFont.new()
	ui_font.font_names = PackedStringArray([
		"Malgun Gothic",
		"NanumGothic",
		"Apple SD Gothic Neo",
		"Noto Sans CJK KR",
		"Segoe UI",
		"Arial"
	])
	_load_game_data()
	load_save()


func _process(delta: float) -> void:
	play_time_sec += delta
	if fever_time_left > 0.0:
		fever_time_left = maxf(0.0, fever_time_left - delta)

	# Update rolling $/s
	var now := play_time_sec
	while _recent_earnings.size() > 0 and (now - float(_recent_earnings[0]["t"])) > 5.0:
		_recent_earnings.pop_front()
	var sum := 0.0
	for entry in _recent_earnings:
		sum += float(entry["v"])
	money_per_sec = sum / 5.0

	_autosave_timer += delta
	if _autosave_timer >= 15.0:
		_autosave_timer = 0.0
		save_game()


func _load_game_data() -> void:
	var parts_data: Variant = _read_json("res://data/parts.json")
	if typeof(parts_data) == TYPE_DICTIONARY and parts_data.has("parts"):
		for p in parts_data["parts"]:
			parts_by_id[str(p["id"])] = p

	var scraps_data: Variant = _read_json("res://data/scraps.json")
	if typeof(scraps_data) == TYPE_DICTIONARY and scraps_data.has("scraps"):
		scraps_list = scraps_data["scraps"]

	var skills_data: Variant = _read_json("res://data/skills.json")
	if typeof(skills_data) == TYPE_DICTIONARY and skills_data.has("skills"):
		skills_list = skills_data["skills"]
		for s in skills_list:
			skills_by_id[str(s["id"])] = s


func _read_json(path: String) -> Variant:
	if not FileAccess.file_exists(path):
		push_error("Missing data file: " + path)
		return {}
	var f := FileAccess.open(path, FileAccess.READ)
	var text := f.get_as_text()
	f.close()
	var parsed: Variant = JSON.parse_string(text)
	if parsed == null:
		push_error("Invalid JSON in: " + path)
		return {}
	return parsed


func add_money(amount: float) -> void:
	if amount <= 0.0:
		return
	var final_amount := amount
	if is_fever_active():
		final_amount *= get_fever_money_mult()
	money += final_amount
	total_earned += final_amount
	_recent_earnings.append({"t": play_time_sec, "v": final_amount})
	state_changed.emit()


func add_fever_charge(amount: float) -> void:
	if is_fever_active():
		return
	fever_charge = clampf(fever_charge + amount, 0.0, 100.0)


func trigger_fever() -> bool:
	if fever_charge < 100.0 or is_fever_active():
		return false
	fever_charge = 0.0
	fever_time_left = get_fever_duration()
	SfxManager.play_fever()
	fever_started.emit()
	state_changed.emit()
	return true


func is_fever_active() -> bool:
	return fever_time_left > 0.0


func get_skill_level(skill_id: String) -> int:
	return int(skill_levels.get(skill_id, 0))


func get_skill_cost(skill_id: String) -> float:
	if not skills_by_id.has(skill_id):
		return 999999.0
	var s: Dictionary = skills_by_id[skill_id]
	var lv := get_skill_level(skill_id)
	return floor(float(s["base_cost"]) * pow(float(s["cost_mult"]), lv))


func is_skill_unlocked(skill_id: String) -> bool:
	if not skills_by_id.has(skill_id):
		return false
	var parent_id := str(skills_by_id[skill_id].get("parent", ""))
	if parent_id == "":
		return true
	return get_skill_level(parent_id) > 0


func can_buy_skill(skill_id: String) -> bool:
	if not is_skill_unlocked(skill_id):
		return false
	var s: Dictionary = skills_by_id[skill_id]
	var lv := get_skill_level(skill_id)
	if lv >= int(s["max_level"]):
		return false
	return money >= get_skill_cost(skill_id)


func buy_skill(skill_id: String) -> bool:
	if not can_buy_skill(skill_id):
		return false
	var cost := get_skill_cost(skill_id)
	money -= cost
	var new_lv := get_skill_level(skill_id) + 1
	skill_levels[skill_id] = new_lv

	var s: Dictionary = skills_by_id[skill_id]
	if str(s["effect"]) == "unlock_tier":
		var t := int(s["value"])
		if t > unlocked_tier:
			unlocked_tier = t
			current_tier = t

	SfxManager.play_buy()
	skill_purchased.emit(skill_id, new_lv)
	state_changed.emit()
	save_game()
	return true


func can_afford_any_skill() -> bool:
	for s in skills_list:
		if can_buy_skill(str(s["id"])):
			return true
	return false


# --- Computed Stats ---

func get_manual_speed_mult() -> float:
	var lv := get_skill_level("driver_speed")
	var mult := 1.0 + lv * 0.35
	if is_fever_active():
		mult *= 1.8
	return mult


func get_cursor_magnet_radius() -> float:
	return 62.0 + get_skill_level("magnet_glove") * 32.0


func get_screw_hover_radius() -> float:
	return 24.0 + get_skill_level("multi_wrench") * 20.0


func get_fever_duration() -> float:
	return 14.0 + get_skill_level("fever_boost") * 3.0


func get_fever_money_mult() -> float:
	return 2.0 + get_skill_level("fever_boost") * 0.5


func has_auto_conveyor() -> bool:
	return get_skill_level("auto_conveyor") > 0


func get_conveyor_interval() -> float:
	var lv := get_skill_level("auto_conveyor")
	var base := maxf(0.45, 2.0 - (lv - 1) * 0.36)
	if is_fever_active():
		base *= 0.45
	return base


func get_robot_arm_level() -> int:
	return get_skill_level("robot_arm")


func get_robot_arm_speed() -> float:
	var lv := get_robot_arm_level()
	if lv <= 0:
		return 0.0
	var spd := 0.85 + (lv - 1) * 0.45
	if is_fever_active():
		spd *= 2.0
	return spd


func get_electromagnet_level() -> int:
	return get_skill_level("electromagnet")


func get_electromagnet_interval() -> float:
	var lv := get_electromagnet_level()
	if lv <= 0:
		return 999.0
	var iv := maxf(0.65, 3.6 - (lv - 1) * 0.65)
	if is_fever_active():
		iv *= 0.5
	return iv


func get_crusher_level() -> int:
	return get_skill_level("hydraulic_crusher")


func get_crusher_interval() -> float:
	var lv := get_crusher_level()
	if lv <= 0:
		return 999.0
	var iv := maxf(0.85, 3.8 - (lv - 1) * 0.68)
	if is_fever_active():
		iv *= 0.5
	return iv


func get_crush_loss_rate() -> float:
	return clampf(0.40 - get_skill_level("precision_blade") * 0.10, 0.0, 0.40)


func get_crush_bonus_drop_mult() -> float:
	return 1.0 + get_skill_level("auto_sifter") * 0.25


func get_smelter_level() -> int:
	return get_skill_level("smelter")


func get_laser_level() -> int:
	return get_skill_level("laser_cutter")


func get_laser_interval() -> float:
	var lv := get_laser_level()
	if lv <= 0:
		return 999.0
	var iv := maxf(0.55, 2.2 - (lv - 1) * 0.45)
	if is_fever_active():
		iv *= 0.5
	return iv


func get_part_value(part_id: String) -> float:
	if not parts_by_id.has(part_id):
		return 1.0
	var p: Dictionary = parts_by_id[part_id]
	var base := float(p["value"])
	var mult := 1.0 + get_skill_level("part_value") * 0.25
	if str(p.get("shape", "")) == "ingot":
		var sm_lv := maxi(get_smelter_level() - 1, 0)
		mult *= (1.0 + sm_lv * 0.25 + get_skill_level("smelt_purity") * 0.35)
	return maxf(1.0, round(base * mult))


func get_rare_gold_chance() -> float:
	return 0.03 + get_skill_level("rare_gold") * 0.08


func pick_random_scrap(tier: int, boss: bool = false) -> Dictionary:
	var pool: Array = []
	for s in scraps_list:
		if int(s["tier"]) == tier and bool(s["is_boss"]) == boss:
			pool.append(s)
	if pool.is_empty():
		return scraps_list[0] if scraps_list.size() > 0 else {}
	return pool[randi() % pool.size()]


func mark_boss_defeated(tier: int) -> void:
	if not bosses_defeated.has(tier):
		bosses_defeated.append(tier)
	boss_defeated.emit(tier)
	if tier == 4 and not ending_reached:
		ending_reached = true
		ending_unlocked.emit()
	save_game()


func format_money(val: float) -> String:
	if val >= 1_000_000_000.0:
		return "$%.2fB" % (val / 1_000_000_000.0)
	if val >= 1_000_000.0:
		return "$%.2fM" % (val / 1_000_000.0)
	if val >= 10_000.0:
		return "$%.1fK" % (val / 1_000.0)
	return "$%d" % int(round(val))


func get_tier_name(t: int) -> String:
	match t:
		1: return "T1. 햇살 드는 1평 공방"
		2: return "T2. 파스텔 창고 공방"
		3: return "T3. 토이·로봇 팩토리"
		4: return "T4. 구름 위 우주 공방"
	return "공방"


func save_game() -> void:
	var payload := {
		"money": money,
		"total_earned": total_earned,
		"unlocked_tier": unlocked_tier,
		"current_tier": current_tier,
		"bosses_defeated": bosses_defeated,
		"skill_levels": skill_levels,
		"total_screws_removed": total_screws_removed,
		"total_scraps_dismantled": total_scraps_dismantled,
		"total_ingots_smelted": total_ingots_smelted,
		"play_time_sec": play_time_sec,
		"ending_reached": ending_reached
	}
	var f := FileAccess.open(SAVE_PATH, FileAccess.WRITE)
	if f:
		f.store_string(JSON.stringify(payload, "  "))
		f.close()


func load_save() -> void:
	if not FileAccess.file_exists(SAVE_PATH):
		return
	var f := FileAccess.open(SAVE_PATH, FileAccess.READ)
	var text := f.get_as_text()
	f.close()
	var parsed: Variant = JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		return
	money = float(parsed.get("money", 0.0))
	total_earned = float(parsed.get("total_earned", 0.0))
	unlocked_tier = int(parsed.get("unlocked_tier", 1))
	current_tier = clampi(int(parsed.get("current_tier", unlocked_tier)), 1, unlocked_tier)
	bosses_defeated = []
	for b in parsed.get("bosses_defeated", []):
		bosses_defeated.append(int(b))
	skill_levels = parsed.get("skill_levels", {})
	total_screws_removed = int(parsed.get("total_screws_removed", 0))
	total_scraps_dismantled = int(parsed.get("total_scraps_dismantled", 0))
	total_ingots_smelted = int(parsed.get("total_ingots_smelted", 0))
	play_time_sec = float(parsed.get("play_time_sec", 0.0))
	ending_reached = bool(parsed.get("ending_reached", false))


func reset_save() -> void:
	money = 0.0
	total_earned = 0.0
	unlocked_tier = 1
	current_tier = 1
	bosses_defeated = []
	skill_levels = {}
	fever_charge = 0.0
	fever_time_left = 0.0
	total_screws_removed = 0
	total_scraps_dismantled = 0
	total_ingots_smelted = 0
	play_time_sec = 0.0
	ending_reached = false
	_recent_earnings.clear()
	money_per_sec = 0.0
	if FileAccess.file_exists(SAVE_PATH):
		DirAccess.remove_absolute(SAVE_PATH)
	state_changed.emit()
