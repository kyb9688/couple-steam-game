extends Control

signal closed

const OUTLINE := Color("#3A2B24")
const NODE_W := 178.0
const NODE_H := 76.0
const CELL_X := 215.0
const CELL_Y := 112.0

var pan_offset: Vector2 = Vector2.ZERO
var _dragging: bool = false
var _drag_start: Vector2 = Vector2.ZERO
var _pan_start: Vector2 = Vector2.ZERO
var hovered_skill_id: String = ""
var _sb_cache: StyleBoxFlat = StyleBoxFlat.new()


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_STOP
	visible = false


func open_tree() -> void:
	visible = true
	pan_offset = Vector2(size.x * 0.5, 118.0)
	queue_redraw()


func close_tree() -> void:
	visible = false
	closed.emit()


func _process(_delta: float) -> void:
	if visible:
		queue_redraw()


func _get_node_rect(s: Dictionary) -> Rect2:
	var gx := float(s["x"])
	var gy := float(s["y"])
	var center := pan_offset + Vector2(gx * CELL_X, gy * CELL_Y)
	return Rect2(center - Vector2(NODE_W, NODE_H) * 0.5, Vector2(NODE_W, NODE_H))


func _gui_input(event: InputEvent) -> void:
	if not visible:
		return

	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_LEFT:
			if mb.pressed:
				# Close button check (top-right)
				var close_rect := Rect2(size.x - 134, 24, 108, 42)
				if close_rect.has_point(mb.position):
					close_tree()
					return

				# Check skill node click
				for s in GameManager.skills_list:
					var sid := str(s["id"])
					if _get_node_rect(s).has_point(mb.position):
						if GameManager.buy_skill(sid):
							queue_redraw()
						return

				_dragging = true
				_drag_start = mb.position
				_pan_start = pan_offset
			else:
				_dragging = false
		elif mb.button_index == MOUSE_BUTTON_WHEEL_UP:
			pan_offset.y += 36.0
		elif mb.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			pan_offset.y -= 36.0

	elif event is InputEventMouseMotion:
		var mm := event as InputEventMouseMotion
		if _dragging:
			pan_offset = _pan_start + (mm.position - _drag_start)
		hovered_skill_id = ""
		for s in GameManager.skills_list:
			if _get_node_rect(s).has_point(mm.position):
				hovered_skill_id = str(s["id"])
				break


func _branch_color(branch: String) -> Color:
	match branch:
		"manual": return Color("#FFD166")
		"automation": return Color("#4EA8DE")
		"value": return Color("#52B788")
		"tier": return Color("#F28482")
	return Color("#E9D8A6")


func _draw_cozy_box(rect: Rect2, fill: Color, border: Color = OUTLINE, radius: int = 12, bw: int = 3) -> void:
	_sb_cache.bg_color = fill
	_sb_cache.border_color = border
	_sb_cache.set_border_width_all(bw)
	_sb_cache.set_corner_radius_all(radius)
	draw_style_box(_sb_cache, rect)


func _draw_centered(pos: Vector2, text: String, fsize: int, col: Color) -> void:
	var font := GameManager.ui_font
	var sz := font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, fsize)
	draw_string(font, Vector2(pos.x - sz.x * 0.5, pos.y + sz.y * 0.3), text, HORIZONTAL_ALIGNMENT_LEFT, -1, fsize, col)


func _draw() -> void:
	if not visible:
		return

	# Dim overlay + cozy parchment backdrop
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.18, 0.14, 0.11, 0.72))
	var panel := Rect2(20, 16, size.x - 40, size.y - 32)
	_draw_cozy_box(panel, Color("#FFF8EC"), OUTLINE, 20, 4)

	# Prerequisite lines first
	for s in GameManager.skills_list:
		var parent_id := str(s.get("parent", ""))
		if parent_id == "" or not GameManager.skills_by_id.has(parent_id):
			continue
		var p_rect := _get_node_rect(GameManager.skills_by_id[parent_id])
		var c_rect := _get_node_rect(s)
		var p_center := p_rect.position + p_rect.size * 0.5
		var c_center := c_rect.position + c_rect.size * 0.5
		var unlocked := GameManager.is_skill_unlocked(str(s["id"]))
		var line_col := Color("#D97706") if unlocked else Color(0.65, 0.60, 0.55, 0.55)
		draw_line(p_center, c_center, line_col, 4.5 if unlocked else 3.0)

	# Draw skill nodes
	for s in GameManager.skills_list:
		var sid := str(s["id"])
		var rect := _get_node_rect(s)
		var lv := GameManager.get_skill_level(sid)
		var max_lv := int(s["max_level"])
		var unlocked := GameManager.is_skill_unlocked(sid)
		var can_buy := GameManager.can_buy_skill(sid)
		var is_max := lv >= max_lv
		var bcol := _branch_color(str(s["branch"]))

		var fill := Color("#E5E0D8")
		if is_max:
			fill = bcol
		elif lv > 0:
			fill = bcol.lerp(Color("#FFFDF7"), 0.35)
		elif unlocked:
			fill = Color("#FFFDF7")

		var border := OUTLINE
		var bw := 3
		if can_buy:
			var pulse := 0.5 + 0.5 * sin(GameManager.play_time_sec * 7.0)
			border = Color("#FF9F1C").lerp(OUTLINE, pulse * 0.35)
			bw = 4

		_draw_cozy_box(rect, fill, border, 12, bw)
		# Branch color top strip
		draw_rect(Rect2(rect.position.x + 6, rect.position.y + 5, rect.size.x - 12, 6), bcol if unlocked else Color("#B8B2A6"))

		var text_col := OUTLINE if unlocked else Color(0.45, 0.42, 0.40)
		_draw_centered(Vector2(rect.position.x + rect.size.x * 0.5, rect.position.y + 30), str(s["name"]), 14, text_col)

		var sub_text := "MAX (%d/%d)" % [lv, max_lv] if is_max else ("Lv.%d/%d · %s" % [lv, max_lv, GameManager.format_money(GameManager.get_skill_cost(sid))])
		var sub_col := Color("#1B7F5C") if (can_buy or is_max) else Color("#8D6E63")
		if not unlocked:
			sub_text = "선행 스킬 필요"
			sub_col = Color(0.5, 0.48, 0.45)
		_draw_centered(Vector2(rect.position.x + rect.size.x * 0.5, rect.position.y + 54), sub_text, 13, sub_col)

	# Top header bar inside overlay
	_draw_cozy_box(Rect2(34, 24, 520, 44), Color("#FFE8C2"), OUTLINE, 12, 3)
	draw_string(
		GameManager.ui_font,
		Vector2(50, 52),
		"공방 스킬트리 (빈 곳 드래그로 이동 · 클릭하여 즉시 구매)  |  보유: %s" % GameManager.format_money(GameManager.money),
		HORIZONTAL_ALIGNMENT_LEFT, -1, 14, OUTLINE
	)

	# Close button
	var close_rect := Rect2(size.x - 134, 24, 108, 42)
	_draw_cozy_box(close_rect, Color("#F28482"), OUTLINE, 12, 3)
	_draw_centered(close_rect.position + close_rect.size * 0.5, "닫기 (Tab)", 14, Color("#FFFDF7"))

	# Bottom description bar
	var desc_rect := Rect2(34, size.y - 76, size.x - 68, 48)
	_draw_cozy_box(desc_rect, Color("#FFFDF7"), OUTLINE, 12, 3)
	var desc_msg := "노란색: 수동 도구  |  파란색: 자동화 기계  |  초록색: 부품·제련 가치  |  빨간색: 공방 티어 해금 (스킬에 마우스를 올려 상세 확인)"
	if hovered_skill_id != "" and GameManager.skills_by_id.has(hovered_skill_id):
		var hs: Dictionary = GameManager.skills_by_id[hovered_skill_id]
		var hlv := GameManager.get_skill_level(hovered_skill_id)
		var hmax := int(hs["max_level"])
		desc_msg = "[%s] %s  (현재 Lv.%d/%d)" % [str(hs["name"]), str(hs["desc"]), hlv, hmax]
	draw_string(GameManager.ui_font, Vector2(52, size.y - 46), desc_msg, HORIZONTAL_ALIGNMENT_LEFT, -1, 15, OUTLINE)
