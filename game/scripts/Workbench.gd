extends Control

signal scrap_cleared(was_boss: bool, tier: int)

const OUTLINE := Color("#3A2B24")
const WHITE_CREAM := Color("#FFFDF7")

var input_blocked: bool = false

# Active scrap state (Dictionary or empty)
var active_scrap: Dictionary = {}
var scrap_enter_t: float = 1.0
var scrap_shake: float = 0.0
var scrap_scale_pop: float = 1.0

# Floating / physics objects
var parts: Array = []          # Dropped collectible parts
var smelting_parts: Array = [] # Parts flying into the smelter
var particles: Array = []      # Sparks / dust / stars
var popups: Array = []         # Floating $ / label popups
var rings: Array = []          # Expanding shockwaves (magnet / crush)

# Visual timers & animations
var belt_offset: float = 0.0
var auto_spawn_timer: float = 0.0
var magnet_timer: float = 0.0
var crusher_timer: float = 0.0
var crusher_anim: float = 0.0 # 0=idle, >0 slamming animation
var crusher_slammed: bool = false
var laser_timer: float = 0.0
var laser_flash: float = 0.0
var smelter_pop: float = 1.0
var screen_shake: float = 0.0
var robot_arm_tip: Vector2 = Vector2(260, 210)

# Audio/combo throttling
var _screw_tick_cd: float = 0.0
var _collect_combo: int = 0
var _combo_reset_timer: float = 0.0
var _tex_cache: Dictionary = {}
var _sb_cache: StyleBoxFlat = StyleBoxFlat.new()


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_STOP
	spawn_scrap(false)


func _process(delta: float) -> void:
	var belt_spd := 45.0
	if GameManager.has_auto_conveyor():
		belt_spd = 75.0 + GameManager.get_skill_level("auto_conveyor") * 24.0
	if GameManager.is_fever_active():
		belt_spd *= 2.2
	belt_offset = fmod(belt_offset + belt_spd * delta, 48.0)

	screen_shake = maxf(0.0, screen_shake - delta * 26.0)
	scrap_shake = maxf(0.0, scrap_shake - delta * 22.0)
	scrap_scale_pop = lerpf(scrap_scale_pop, 1.0, minf(delta * 14.0, 1.0))
	smelter_pop = lerpf(smelter_pop, 1.0, minf(delta * 12.0, 1.0))
	laser_flash = maxf(0.0, laser_flash - delta * 4.5)
	_screw_tick_cd = maxf(0.0, _screw_tick_cd - delta)

	if _combo_reset_timer > 0.0:
		_combo_reset_timer -= delta
		if _combo_reset_timer <= 0.0:
			_collect_combo = 0

	# Auto spawn next scrap if conveyor unlocked
	if active_scrap.is_empty():
		if GameManager.has_auto_conveyor():
			auto_spawn_timer += delta
			if auto_spawn_timer >= GameManager.get_conveyor_interval():
				auto_spawn_timer = 0.0
				spawn_scrap(false)
	else:
		scrap_enter_t = minf(1.0, scrap_enter_t + delta * 4.2)
		_update_manual_unscrew(delta)
		_update_robot_arm(delta)
		_update_crusher(delta)
		_update_laser(delta)

	_update_electromagnet(delta)
	_update_parts(delta)
	_update_smelting_parts(delta)
	_update_particles(delta)
	_update_popups(delta)
	_update_rings(delta)

	queue_redraw()


func _get_desk_center() -> Vector2:
	return Vector2(size.x * 0.45, size.y * 0.54)


func _get_smelter_pos() -> Vector2:
	return Vector2(size.x * 0.84, size.y * 0.54)


func _get_magnet_pos() -> Vector2:
	return Vector2(size.x * 0.67, size.y * 0.17)


func _get_arm_base() -> Vector2:
	return Vector2(size.x * 0.23, size.y * 0.19)


func spawn_scrap(force_boss: bool = false) -> void:
	if force_boss:
		active_scrap.clear()
	if not active_scrap.is_empty():
		return
	var data := GameManager.pick_random_scrap(GameManager.current_tier, force_boss)
	if data.is_empty():
		return

	var w := float(data["width"])
	var h := float(data["height"])
	var screw_count := int(data["screws"])
	var screws: Array = []

	# Arrange screws evenly around perimeter of the scrap body
	var margin_x := w * 0.38
	var margin_y := h * 0.35
	for i in range(screw_count):
		var angle := (TAU * float(i) / float(screw_count)) - PI * 0.75
		var sx := clampf(cos(angle) * margin_x * 1.15, -margin_x, margin_x)
		var sy := clampf(sin(angle) * margin_y * 1.15, -margin_y, margin_y)
		screws.append({
			"offset": Vector2(sx, sy),
			"progress": 0.0,
			"max_time": float(data["screw_time"]),
			"spin": 0.0,
			"done": false
		})

	active_scrap = {
		"data": data,
		"screws": screws,
		"w": w,
		"h": h
	}
	scrap_enter_t = 0.0
	scrap_scale_pop = 1.08
	if bool(data["is_boss"]):
		SfxManager.play_boss()
		_add_popup(_get_desk_center() + Vector2(0, -h * 0.75), str(data["name"]) + " 등장!", Color("#EF476F"), 24)


func _update_manual_unscrew(delta: float) -> void:
	if input_blocked or scrap_enter_t < 0.85 or active_scrap.is_empty():
		return
	var mouse := get_local_mouse_position()
	var center := _get_desk_center()
	var hover_r := GameManager.get_screw_hover_radius()
	var speed := GameManager.get_manual_speed_mult()
	if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		speed *= 1.35

	var screws: Array = active_scrap["screws"]
	var touched_any := false
	for sc in screws:
		if bool(sc["done"]):
			continue
		var world_pos: Vector2 = center + Vector2(sc["offset"])
		if mouse.distance_to(world_pos) <= hover_r + 14.0:
			touched_any = true
			_advance_screw(sc, delta * speed, world_pos)

	if touched_any and _screw_tick_cd <= 0.0:
		_screw_tick_cd = 0.085
		SfxManager.play_screw_tick(0.5)


func _update_robot_arm(delta: float) -> void:
	var lv := GameManager.get_robot_arm_level()
	if lv <= 0 or active_scrap.is_empty() or scrap_enter_t < 0.9:
		robot_arm_tip = robot_arm_tip.lerp(_get_arm_base() + Vector2(40, 70), minf(delta * 6.0, 1.0))
		return
	var center := _get_desk_center()
	var screws: Array = active_scrap["screws"]
	var target_sc: Variant = null
	var target_pos := Vector2.ZERO
	for sc in screws:
		if not bool(sc["done"]):
			target_sc = sc
			target_pos = center + Vector2(sc["offset"])
			break
	if target_sc == null:
		return
	robot_arm_tip = robot_arm_tip.lerp(target_pos, minf(delta * 10.0, 1.0))
	if robot_arm_tip.distance_to(target_pos) < 18.0:
		_advance_screw(target_sc, delta * GameManager.get_robot_arm_speed(), target_pos)


func _advance_screw(sc: Dictionary, amount: float, world_pos: Vector2) -> void:
	sc["progress"] = float(sc["progress"]) + amount
	sc["spin"] = float(sc["spin"]) + amount * 16.0
	scrap_shake = maxf(scrap_shake, 2.2)
	if randf() < 0.28:
		_spawn_spark(world_pos, Color("#FFD166"), 1)
	if float(sc["progress"]) >= float(sc["max_time"]):
		sc["done"] = true
		GameManager.total_screws_removed += 1
		GameManager.add_fever_charge(2.6)
		SfxManager.play_screw_pop()
		_spawn_spark(world_pos, Color("#FFB703"), 7)
		_spawn_part("screw", world_pos)
		_check_all_screws_done()


func _check_all_screws_done() -> void:
	if active_scrap.is_empty():
		return
	for sc in active_scrap["screws"]:
		if not bool(sc["done"]):
			return
	_burst_active_scrap(1.0, false, "분해 완료!")


func _update_crusher(delta: float) -> void:
	var lv := GameManager.get_crusher_level()
	if lv <= 0:
		return
	if crusher_anim > 0.0:
		crusher_anim = maxf(0.0, crusher_anim - delta * 2.5)
		if crusher_anim <= 0.62 and not crusher_slammed:
			crusher_slammed = true
			if not active_scrap.is_empty():
				var keep_ratio := (1.0 - GameManager.get_crush_loss_rate()) * GameManager.get_crush_bonus_drop_mult()
				SfxManager.play_crush()
				screen_shake = 14.0
				rings.append({
					"pos": _get_desk_center(),
					"r": 25.0,
					"max_r": 210.0,
					"color": Color("#FF9F1C"),
					"alpha": 0.9
				})
				var loss_pct := int(round(GameManager.get_crush_loss_rate() * 100.0))
				var msg := "콰앙!! 유압 파쇄!" if loss_pct <= 0 else ("콰앙! 파쇄 (손실 %d%%)" % loss_pct)
				_burst_active_scrap(keep_ratio, true, msg)
		return

	if active_scrap.is_empty() or scrap_enter_t < 0.95:
		return
	crusher_timer += delta
	if crusher_timer >= GameManager.get_crusher_interval():
		crusher_timer = 0.0
		crusher_anim = 1.0
		crusher_slammed = false


func _update_laser(delta: float) -> void:
	var lv := GameManager.get_laser_level()
	if lv <= 0 or active_scrap.is_empty() or scrap_enter_t < 0.95:
		return
	laser_timer += delta
	if laser_timer >= GameManager.get_laser_interval():
		laser_timer = 0.0
		laser_flash = 1.0
		SfxManager.play_laser()
		screen_shake = maxf(screen_shake, 6.0)
		_burst_active_scrap(1.25 * GameManager.get_crush_bonus_drop_mult(), false, "레이저 정밀 절단!")


func _burst_active_scrap(drop_mult: float, is_crushed: bool, label_text: String) -> void:
	if active_scrap.is_empty():
		return
	var data: Dictionary = active_scrap["data"]
	var center := _get_desk_center()
	var is_boss := bool(data["is_boss"])
	var tier := int(data["tier"])

	if not is_crushed:
		SfxManager.play_scrap_open()
		screen_shake = maxf(screen_shake, 6.5)

	_spawn_spark(center, Color(str(data["color"])), 18)
	_spawn_spark(center, Color("#FFD166"), 10)
	_add_popup(center + Vector2(0, -65), label_text, Color("#FF6B6B") if is_crushed else Color("#2A9D8F"), 20)

	# Spawn parts from drop table
	for entry in data["drops"]:
		var part_id := str(entry["part"])
		var raw_count := float(entry["count"]) * drop_mult
		var count := int(floor(raw_count))
		if randf() < (raw_count - float(count)):
			count += 1
		count = maxi(1, count)
		for _i in range(count):
			_spawn_part(part_id, center + Vector2(randf_range(-40, 40), randf_range(-25, 25)))

	# Rare bonus gold chip roll
	if randf() < GameManager.get_rare_gold_chance():
		_spawn_part("gold_chip", center)
		_add_popup(center + Vector2(0, -95), "★ 황금 칩 발견!", Color("#FFB703"), 20)

	GameManager.total_scraps_dismantled += 1
	GameManager.add_fever_charge(12.0 if not is_boss else 35.0)
	active_scrap.clear()

	if is_boss:
		GameManager.mark_boss_defeated(tier)
	scrap_cleared.emit(is_boss, tier)


func _spawn_part(part_id: String, pos: Vector2) -> void:
	if not GameManager.parts_by_id.has(part_id):
		return
	var pdata: Dictionary = GameManager.parts_by_id[part_id]
	parts.append({
		"id": part_id,
		"data": pdata,
		"pos": pos,
		"vel": Vector2(randf_range(-240.0, 240.0), randf_range(-300.0, -110.0)),
		"floor_y": _get_desk_center().y + randf_range(55.0, 128.0),
		"rot": randf_range(-0.6, 0.6),
		"age": 0.0,
		"magnetized": false
	})


func _update_electromagnet(delta: float) -> void:
	if GameManager.get_electromagnet_level() <= 0:
		return
	if parts.is_empty():
		return
	magnet_timer += delta
	if magnet_timer >= GameManager.get_electromagnet_interval():
		magnet_timer = 0.0
		SfxManager.play_magnet()
		rings.append({
			"pos": _get_magnet_pos(),
			"r": 20.0,
			"max_r": 460.0,
			"color": Color("#4ECDC4"),
			"alpha": 0.75
		})
		for p in parts:
			p["magnetized"] = true


func _update_parts(delta: float) -> void:
	var mouse := get_local_mouse_position()
	var collect_r := GameManager.get_cursor_magnet_radius()
	var smelter_on := GameManager.get_smelter_level() > 0
	var smelter_pos := _get_smelter_pos()
	var desk_min_x := 80.0
	var desk_max_x := size.x * 0.74

	var i := parts.size() - 1
	while i >= 0:
		var p: Dictionary = parts[i]
		p["age"] = float(p["age"]) + delta
		var pos: Vector2 = p["pos"]
		var vel: Vector2 = p["vel"]

		# Check cursor hover pickup
		if not input_blocked and float(p["age"]) > 0.14 and mouse.distance_to(pos) <= collect_r:
			p["magnetized"] = true

		if bool(p["magnetized"]):
			var target := smelter_pos if (smelter_on and bool(p["data"].get("smeltable", false))) else Vector2(size.x * 0.92, 36.0)
			var dir := (target - pos)
			if dir.length() < 34.0:
				_collect_part(p, smelter_on)
				parts.remove_at(i)
				i -= 1
				continue
			pos += dir.normalized() * 880.0 * delta
			p["pos"] = pos
		else:
			vel.y += 780.0 * delta
			pos += vel * delta
			var fy := float(p["floor_y"])
			if pos.y > fy:
				pos.y = fy
				vel.y = -absf(vel.y) * 0.42
				vel.x *= 0.72
			if pos.x < desk_min_x:
				pos.x = desk_min_x
				vel.x = absf(vel.x) * 0.6
			elif pos.x > desk_max_x:
				pos.x = desk_max_x
				vel.x = -absf(vel.x) * 0.6
			p["pos"] = pos
			p["vel"] = vel
		i -= 1


func _collect_part(p: Dictionary, smelter_on: bool) -> void:
	var pid := str(p["id"])
	var pdata: Dictionary = p["data"]
	var pos: Vector2 = p["pos"]

	if smelter_on and bool(pdata.get("smeltable", false)):
		var ingot_id := str(pdata.get("smelt_to", "copper_ingot"))
		var ingot_val := GameManager.get_part_value(ingot_id)
		GameManager.add_money(ingot_val)
		GameManager.total_ingots_smelted += 1
		smelter_pop = 1.22
		SfxManager.play_smelt()
		_spawn_spark(_get_smelter_pos() + Vector2(0, -25), Color("#FFBE0B"), 6)
		var ingot_name := str(GameManager.parts_by_id.get(ingot_id, {}).get("name", "주괴"))
		_add_popup(_get_smelter_pos() + Vector2(randf_range(-25, 25), -58), "+%s (%s)" % [GameManager.format_money(ingot_val), ingot_name], Color("#FF9F1C"), 16)
	else:
		var val := GameManager.get_part_value(pid)
		GameManager.add_money(val)
		SfxManager.play_collect(_collect_combo)
		_collect_combo = mini(_collect_combo + 1, 10)
		_combo_reset_timer = 0.75
		_add_popup(pos + Vector2(0, -18), "+" + GameManager.format_money(val), Color("#2A9D8F"), 16)


func _update_smelting_parts(_delta: float) -> void:
	pass


func _spawn_spark(pos: Vector2, color: Color, count: int) -> void:
	for _i in range(count):
		var ang := randf_range(0.0, TAU)
		var spd := randf_range(70.0, 260.0)
		particles.append({
			"pos": pos,
			"vel": Vector2(cos(ang), sin(ang)) * spd,
			"color": color,
			"size": randf_range(4.0, 9.0),
			"life": randf_range(0.25, 0.55),
			"max_life": 0.55
		})


func _update_particles(delta: float) -> void:
	var i := particles.size() - 1
	while i >= 0:
		var pt: Dictionary = particles[i]
		pt["life"] = float(pt["life"]) - delta
		if float(pt["life"]) <= 0.0:
			particles.remove_at(i)
		else:
			var vel: Vector2 = pt["vel"]
			vel.y += 420.0 * delta
			pt["vel"] = vel
			pt["pos"] = Vector2(pt["pos"]) + vel * delta
		i -= 1


func _add_popup(pos: Vector2, text: String, color: Color, fsize: int = 16) -> void:
	popups.append({
		"pos": pos,
		"text": text,
		"color": color,
		"size": fsize,
		"life": 0.95,
		"max_life": 0.95
	})


func _update_popups(delta: float) -> void:
	var i := popups.size() - 1
	while i >= 0:
		var pp: Dictionary = popups[i]
		pp["life"] = float(pp["life"]) - delta
		if float(pp["life"]) <= 0.0:
			popups.remove_at(i)
		else:
			pp["pos"] = Vector2(pp["pos"]) + Vector2(0, -48.0 * delta)
		i -= 1


func _update_rings(delta: float) -> void:
	var i := rings.size() - 1
	while i >= 0:
		var rg: Dictionary = rings[i]
		rg["r"] = float(rg["r"]) + (float(rg["max_r"]) - float(rg["r"])) * minf(delta * 7.0, 1.0)
		rg["alpha"] = float(rg["alpha"]) - delta * 1.9
		if float(rg["alpha"]) <= 0.02:
			rings.remove_at(i)
		i -= 1


# =========================================================
# COZY BRIGHT WORKSHOP DRAWING
# =========================================================

func _draw() -> void:
	var shake_vec := Vector2.ZERO
	if screen_shake > 0.2:
		shake_vec = Vector2(randf_range(-screen_shake, screen_shake), randf_range(-screen_shake, screen_shake))
	draw_set_transform(shake_vec, 0.0, Vector2.ONE)

	_draw_workshop_background()
	_draw_conveyor_slot()
	_draw_smelter_slot()
	_draw_magnet_slot()
	_draw_active_scrap()
	_draw_robot_arm_slot()
	_draw_crusher_slot()
	_draw_laser_slot()
	_draw_parts()
	_draw_rings()
	_draw_particles()
	_draw_popups()
	_draw_cursor_ring()
	_draw_onboarding_hint()

	if GameManager.is_fever_active():
		_draw_fever_border()

	draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)


func _draw_cozy_box(rect: Rect2, fill: Color, border: Color = OUTLINE, radius: int = 14, border_w: int = 3) -> void:
	_sb_cache.bg_color = fill
	_sb_cache.border_color = border
	_sb_cache.set_border_width_all(border_w)
	_sb_cache.set_corner_radius_all(radius)
	draw_style_box(_sb_cache, rect)


func _draw_centered_text(pos: Vector2, text: String, fsize: int, color: Color) -> void:
	var font := GameManager.ui_font
	var sz := font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, fsize)
	draw_string(font, Vector2(pos.x - sz.x * 0.5, pos.y + sz.y * 0.3), text, HORIZONTAL_ALIGNMENT_LEFT, -1, fsize, color)


func _draw_workshop_background() -> void:
	var wall_col := Color("#FFF6E6")
	var desk_col := Color("#E5C39C")
	var mat_col := Color("#98C1A8")
	match GameManager.current_tier:
		2:
			wall_col = Color("#EBF8F2")
			desk_col = Color("#D8E2DC")
			mat_col = Color("#7FB5B5")
		3:
			wall_col = Color("#FFF9DB")
			desk_col = Color("#F5D796")
			mat_col = Color("#7DA7D9")
		4:
			wall_col = Color("#EEF4FF")
			desk_col = Color("#C8D8F8")
			mat_col = Color("#8E9AAF")

	# Back wall
	draw_rect(Rect2(Vector2.ZERO, size), wall_col)

	# Cozy sunny window / pegboard stripes
	for x in range(40, int(size.x), 64):
		draw_line(Vector2(x, 0), Vector2(x, size.y * 0.36), Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.05), 2.0)

	# Wooden workbench surface
	var desk_rect := Rect2(0, size.y * 0.34, size.x, size.y * 0.66)
	draw_rect(desk_rect, desk_col)
	draw_line(Vector2(0, size.y * 0.34), Vector2(size.x, size.y * 0.34), OUTLINE, 4.0)

	# Cutting / disassembly mat in the center
	var mat_rect := Rect2(size.x * 0.14, size.y * 0.38, size.x * 0.62, size.y * 0.52)
	_draw_cozy_box(mat_rect, mat_col, OUTLINE, 22, 4)

	# Subtle grid lines on cozy mat
	var grid_col := Color(1, 1, 1, 0.18)
	for gx in range(int(mat_rect.position.x) + 40, int(mat_rect.end.x), 40):
		draw_line(Vector2(gx, mat_rect.position.y + 8), Vector2(gx, mat_rect.end.y - 8), grid_col, 1.5)
	for gy in range(int(mat_rect.position.y) + 40, int(mat_rect.end.y), 40):
		draw_line(Vector2(mat_rect.position.x + 8, gy), Vector2(mat_rect.end.x - 8, gy), grid_col, 1.5)


func _draw_conveyor_slot() -> void:
	var belt_rect := Rect2(-10, size.y * 0.48, size.x * 0.17, size.y * 0.14)
	var active := GameManager.has_auto_conveyor()
	_draw_cozy_box(belt_rect, Color("#5C677D") if active else Color("#B8B2A6"), OUTLINE, 10, 3)

	if active:
		var stripe_x := belt_rect.position.x + fmod(belt_offset, 28.0)
		while stripe_x < belt_rect.end.x - 10:
			draw_line(
				Vector2(stripe_x, belt_rect.position.y + 8),
				Vector2(stripe_x + 12, belt_rect.position.y + belt_rect.size.y * 0.5),
				Color("#FFD166"), 3.0
			)
			draw_line(
				Vector2(stripe_x + 12, belt_rect.position.y + belt_rect.size.y * 0.5),
				Vector2(stripe_x, belt_rect.end.y - 8),
				Color("#FFD166"), 3.0
			)
			stripe_x += 28.0

	_draw_centered_text(
		Vector2(belt_rect.position.x + belt_rect.size.x * 0.52, belt_rect.end.y + 18),
		"자동 컨베이어 ON" if active else "수동 투입구",
		13, OUTLINE
	)


func _draw_robot_arm_slot() -> void:
	var base := _get_arm_base()
	var unlocked := GameManager.get_robot_arm_level() > 0
	if not unlocked:
		_draw_cozy_box(Rect2(base - Vector2(56, 20), Vector2(112, 40)), Color(1, 1, 1, 0.35), Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.25), 10, 2)
		_draw_centered_text(base, "슬롯: 로봇 팔", 12, Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.5))
		return

	var mid := (base + robot_arm_tip) * 0.5 + Vector2(-28, -32)
	draw_line(base, mid, OUTLINE, 14.0)
	draw_line(base, mid, Color("#4EA8DE"), 8.0)
	draw_line(mid, robot_arm_tip, OUTLINE, 12.0)
	draw_line(mid, robot_arm_tip, Color("#FFD166"), 6.0)
	draw_circle(base, 16.0, OUTLINE)
	draw_circle(base, 12.0, Color("#4EA8DE"))
	draw_circle(mid, 10.0, OUTLINE)
	draw_circle(mid, 6.0, WHITE_CREAM)
	draw_circle(robot_arm_tip, 9.0, OUTLINE)
	draw_circle(robot_arm_tip, 5.5, Color("#EF476F"))


func _draw_crusher_slot() -> void:
	var center := _get_desk_center()
	var unlocked := GameManager.get_crusher_level() > 0
	var top_pos := Vector2(center.x, size.y * 0.12)
	if not unlocked:
		_draw_cozy_box(Rect2(top_pos - Vector2(78, 22), Vector2(156, 44)), Color(1, 1, 1, 0.35), Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.25), 10, 2)
		_draw_centered_text(top_pos, "슬롯: 유압 파쇄기", 12, Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.5))
		return

	# Compute press Y position based on slam animation
	var head_y := size.y * 0.16
	if crusher_anim > 0.62:
		# Slam down fast!
		var t := (1.0 - crusher_anim) / 0.38
		head_y = lerpf(size.y * 0.16, center.y - 35.0, t * t)
	elif crusher_anim > 0.0:
		# Lift back up smoothly
		var t := crusher_anim / 0.62
		head_y = lerpf(size.y * 0.16, center.y - 35.0, t)

	# Piston rods
	draw_rect(Rect2(center.x - 52, 0, 18, head_y), Color("#94A3B8"))
	draw_rect(Rect2(center.x + 34, 0, 18, head_y), Color("#94A3B8"))
	draw_line(Vector2(center.x - 52, 0), Vector2(center.x - 52, head_y), OUTLINE, 3.0)
	draw_line(Vector2(center.x + 52, 0), Vector2(center.x + 52, head_y), OUTLINE, 3.0)

	# Chunky cozy press head
	var head_rect := Rect2(center.x - 105, head_y - 26, 210, 48)
	_draw_cozy_box(head_rect, Color("#FF7B54"), OUTLINE, 12, 4)
	# Hazard stripes
	draw_rect(Rect2(head_rect.position.x + 12, head_rect.end.y - 14, head_rect.size.x - 24, 9), Color("#FFD166"))

	# Charge progress bar on press head
	var ratio := clampf(crusher_timer / maxf(GameManager.get_crusher_interval(), 0.1), 0.0, 1.0)
	draw_rect(Rect2(head_rect.position.x + 14, head_rect.position.y + 8, (head_rect.size.x - 28) * ratio, 8), Color("#FFFDF7"))
	var loss_pct := int(round(GameManager.get_crush_loss_rate() * 100.0))
	_draw_centered_text(Vector2(center.x, head_rect.position.y + 24), "유압 파쇄기 Lv.%d (손실 %d%%)" % [GameManager.get_crusher_level(), loss_pct], 12, WHITE_CREAM)


func _draw_laser_slot() -> void:
	if GameManager.get_laser_level() <= 0:
		return
	var center := _get_desk_center()
	var left_em := Vector2(size.x * 0.17, size.y * 0.36)
	var right_em := Vector2(size.x * 0.73, size.y * 0.36)
	draw_circle(left_em, 12.0, OUTLINE)
	draw_circle(left_em, 8.0, Color("#48CAE4"))
	draw_circle(right_em, 12.0, OUTLINE)
	draw_circle(right_em, 8.0, Color("#48CAE4"))
	if laser_flash > 0.05:
		var col := Color(0.28, 0.85, 0.96, laser_flash)
		draw_line(left_em, center + Vector2(30, 10), col, 8.0 * laser_flash)
		draw_line(right_em, center + Vector2(-30, 10), col, 8.0 * laser_flash)
		draw_line(left_em, center + Vector2(30, 10), Color(1, 1, 1, laser_flash), 3.0)
		draw_line(right_em, center + Vector2(-30, 10), Color(1, 1, 1, laser_flash), 3.0)


func _draw_magnet_slot() -> void:
	var pos := _get_magnet_pos()
	var unlocked := GameManager.get_electromagnet_level() > 0
	if not unlocked:
		_draw_cozy_box(Rect2(pos - Vector2(62, 20), Vector2(124, 40)), Color(1, 1, 1, 0.35), Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.25), 10, 2)
		_draw_centered_text(pos, "슬롯: 전자석", 12, Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.5))
		return

	var box := Rect2(pos - Vector2(44, 26), Vector2(88, 52))
	_draw_cozy_box(box, Color("#EF476F"), OUTLINE, 16, 4)
	_draw_cozy_box(Rect2(pos.x - 16, pos.y - 4, 32, 32), Color("#FFF6E6"), OUTLINE, 6, 3)
	draw_rect(Rect2(pos.x - 40, pos.y + 16, 22, 10), Color("#DCE3EA"))
	draw_rect(Rect2(pos.x + 18, pos.y + 16, 22, 10), Color("#DCE3EA"))
	_draw_centered_text(Vector2(pos.x, pos.y - 36), "초강력 전자석 Lv.%d" % GameManager.get_electromagnet_level(), 12, OUTLINE)


func _draw_smelter_slot() -> void:
	var pos := _get_smelter_pos()
	var unlocked := GameManager.get_smelter_level() > 0
	var box_size := Vector2(138, 170) * smelter_pop
	var rect := Rect2(pos - box_size * 0.5, box_size)
	if not unlocked:
		_draw_cozy_box(rect, Color(1, 1, 1, 0.30), Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.25), 20, 3)
		_draw_centered_text(pos + Vector2(0, -9), "슬롯: 황금 용광로", 13, Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.55))
		_draw_centered_text(pos + Vector2(0, 11), "(스킬트리 해금)", 12, Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.45))
		return

	# Warm cozy brick furnace
	_draw_cozy_box(rect, Color("#D96B43"), OUTLINE, 22, 4)
	# Chimney
	_draw_cozy_box(Rect2(pos.x - 24, rect.position.y - 30, 48, 34), Color("#8D5B4C"), OUTLINE, 8, 3)
	# Glowing fire mouth
	var mouth := Rect2(pos.x - 40, pos.y - 10, 80, 62)
	_draw_cozy_box(mouth, Color("#3A2B24"), OUTLINE, 18, 3)
	var pulse := 0.88 + 0.12 * sin(GameManager.play_time_sec * 10.0)
	draw_circle(Vector2(pos.x, pos.y + 28), 24.0 * pulse, Color("#FF9F1C"))
	draw_circle(Vector2(pos.x, pos.y + 32), 14.0 * pulse, Color("#FFD166"))
	_draw_centered_text(Vector2(pos.x, rect.position.y + 24), "황금 용광로 Lv.%d" % GameManager.get_smelter_level(), 13, WHITE_CREAM)


func _draw_active_scrap() -> void:
	if active_scrap.is_empty():
		var c := _get_desk_center()
		_draw_centered_text(c, "고물이 비어 있습니다 — 아래 [다음 고물 올리기] 또는 Space!", 16, Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.65))
		return

	var data: Dictionary = active_scrap["data"]
	var center := _get_desk_center()
	var slide_x := lerpf(-260.0, 0.0, 1.0 - pow(1.0 - scrap_enter_t, 3.0))
	var jitter := Vector2(randf_range(-scrap_shake, scrap_shake), randf_range(-scrap_shake, scrap_shake))
	var draw_center := center + Vector2(slide_x, 0.0) + jitter

	var w := float(active_scrap["w"]) * scrap_scale_pop
	var h := float(active_scrap["h"]) * scrap_scale_pop
	var rect := Rect2(draw_center - Vector2(w, h) * 0.5, Vector2(w, h))

	# Drop shadow
	_draw_cozy_box(Rect2(rect.position + Vector2(6, 10), rect.size), Color(0, 0, 0, 0.16), Color(0, 0, 0, 0), 20, 0)

	# Auto-load PNG if girlfriend placed an asset in res://assets/scraps/<id>_closed.png!
	var tex_path := "res://assets/scraps/%s_closed.png" % str(data["id"])
	var tex: Texture2D = _get_optional_texture(tex_path)
	if tex != null:
		draw_texture_rect(tex, rect, false)
	else:
		# Cozy procedural gadget body
		var body_col := Color(str(data["color"]))
		var acc_col := Color(str(data["accent"]))
		_draw_cozy_box(rect, body_col, OUTLINE, 20, 4)

		# Inner cozy faceplate / screen
		var inner := Rect2(rect.position + Vector2(22, 22), rect.size - Vector2(44, 44))
		_draw_cozy_box(inner, acc_col, OUTLINE, 12, 3)

		# Cute decorative speaker/vent lines
		for l in range(3):
			var ly := inner.position.y + 18 + l * 14
			if ly < inner.end.y - 16:
				draw_line(Vector2(inner.position.x + 16, ly), Vector2(inner.end.x - 16, ly), Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.35), 3.0)

	# Boss badge & Scrap Name tag
	var name_y := rect.position.y - 18.0
	if bool(data["is_boss"]):
		_draw_cozy_box(Rect2(draw_center.x - 120, name_y - 22, 240, 28), Color("#EF476F"), OUTLINE, 10, 3)
		_draw_centered_text(Vector2(draw_center.x, name_y - 8), str(data["name"]), 14, WHITE_CREAM)
	else:
		_draw_centered_text(Vector2(draw_center.x, name_y), str(data["name"]), 15, OUTLINE)

	# Draw interactive Screws
	for sc in active_scrap["screws"]:
		if bool(sc["done"]):
			continue
		var spos := draw_center + Vector2(sc["offset"])
		var prog := clampf(float(sc["progress"]) / maxf(float(sc["max_time"]), 0.05), 0.0, 1.0)
		var r := 13.0 * (1.0 - prog * 0.25)

		draw_circle(spos, r + 3.0, OUTLINE)
		draw_circle(spos, r, Color("#FFD166"))

		# Rotating cross slot
		var sp := float(sc["spin"])
		var d1 := Vector2(cos(sp), sin(sp)) * (r * 0.65)
		var d2 := Vector2(-d1.y, d1.x)
		draw_line(spos - d1, spos + d1, OUTLINE, 2.5)
		draw_line(spos - d2, spos + d2, OUTLINE, 2.5)

		if prog > 0.02:
			draw_arc(spos, r + 7.0, -PI * 0.5, -PI * 0.5 + TAU * prog, 20, Color("#2A9D8F"), 3.5)


func _draw_parts() -> void:
	for p in parts:
		var pos: Vector2 = p["pos"]
		var pdata: Dictionary = p["data"]
		var col := Color(str(pdata["color"]))
		var shape := str(pdata.get("shape", "circle"))
		var is_rare := bool(pdata.get("rare", false))

		if is_rare:
			draw_circle(pos, 18.0 + sin(GameManager.play_time_sec * 8.0) * 2.5, Color(1.0, 0.85, 0.2, 0.35))

		match shape:
			"circle":
				draw_circle(pos, 12.0, OUTLINE)
				draw_circle(pos, 9.0, col)
			"rect":
				_draw_cozy_box(Rect2(pos - Vector2(12, 9), Vector2(24, 18)), col, OUTLINE, 5, 2)
			"capsule":
				_draw_cozy_box(Rect2(pos - Vector2(8, 13), Vector2(16, 26)), col, OUTLINE, 7, 2)
			"diamond":
				var pts := PackedVector2Array([
					pos + Vector2(0, -13),
					pos + Vector2(13, 0),
					pos + Vector2(0, 13),
					pos + Vector2(-13, 0)
				])
				draw_colored_polygon(pts, col)
				pts.append(pts[0])
				draw_polyline(pts, OUTLINE, 2.5)
			_:
				_draw_cozy_box(Rect2(pos - Vector2(14, 9), Vector2(28, 18)), col, OUTLINE, 5, 2)


func _draw_rings() -> void:
	for rg in rings:
		var col: Color = rg["color"]
		col.a = float(rg["alpha"])
		draw_arc(Vector2(rg["pos"]), float(rg["r"]), 0.0, TAU, 48, col, 4.0)


func _draw_particles() -> void:
	for pt in particles:
		var col: Color = pt["color"]
		col.a = clampf(float(pt["life"]) / float(pt["max_life"]), 0.0, 1.0)
		draw_circle(Vector2(pt["pos"]), float(pt["size"]) * col.a, col)


func _draw_popups() -> void:
	for pp in popups:
		var alpha := clampf(float(pp["life"]) / float(pp["max_life"]), 0.0, 1.0)
		var col: Color = pp["color"]
		col.a = alpha
		var shadow := Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, alpha * 0.85)
		var pos: Vector2 = pp["pos"]
		_draw_centered_text(pos + Vector2(1.5, 1.5), str(pp["text"]), int(pp["size"]), shadow)
		_draw_centered_text(pos, str(pp["text"]), int(pp["size"]), col)


func _draw_cursor_ring() -> void:
	if input_blocked:
		return
	var mouse := get_local_mouse_position()
	if not Rect2(Vector2.ZERO, size).has_point(mouse):
		return
	var r := GameManager.get_cursor_magnet_radius()
	draw_arc(mouse, r, 0.0, TAU, 40, Color(OUTLINE.r, OUTLINE.g, OUTLINE.b, 0.16), 2.0)
	var sr := GameManager.get_screw_hover_radius()
	draw_arc(mouse, sr, 0.0, TAU, 24, Color(0.95, 0.75, 0.15, 0.45), 2.0)


func _draw_onboarding_hint() -> void:
	if GameManager.total_scraps_dismantled >= 2:
		return
	var msg := "TIP: 마우스를 노란 나사 위에 올려 풀어보세요! → 튀어나온 부품을 마우스로 쓸어 담으면 돈이 쌓입니다!"
	var box := Rect2(size.x * 0.16, 14, size.x * 0.68, 36)
	_draw_cozy_box(box, Color("#FFFDF7"), OUTLINE, 12, 3)
	_draw_centered_text(box.position + box.size * 0.5, msg, 14, OUTLINE)


func _draw_fever_border() -> void:
	var pulse := 0.55 + 0.45 * sin(GameManager.play_time_sec * 12.0)
	var col := Color(1.0, 0.72, 0.02, pulse)
	draw_rect(Rect2(4, 4, size.x - 8, size.y - 8), col, false, 8.0)


func _get_optional_texture(path: String) -> Texture2D:
	if _tex_cache.has(path):
		return _tex_cache[path]
	if ResourceLoader.exists(path):
		var tex: Texture2D = load(path)
		_tex_cache[path] = tex
		return tex
	_tex_cache[path] = null
	return null
