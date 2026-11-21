extends Control

const WorkbenchScript := preload("res://scripts/Workbench.gd")
const SkillTreeScript := preload("res://scripts/SkillTreeOverlay.gd")

const OUTLINE := Color("#3A2B24")

var workbench
var skill_tree

var lbl_money: Label
var lbl_mps: Label
var lbl_tier: Label
var btn_skill: Button
var btn_boss: Button
var btn_spawn: Button
var btn_fever: Button
var btn_mute: Button
var btn_prev_tier: Button
var btn_next_tier: Button

var debug_panel: PanelContainer
var ending_panel: PanelContainer
var lbl_ending_stats: Label


func _ready() -> void:
	_setup_theme()
	_build_ui()
	GameManager.state_changed.connect(_refresh_hud)
	GameManager.ending_unlocked.connect(_show_ending_card)
	_refresh_hud()


func _setup_theme() -> void:
	var t := Theme.new()
	t.default_font = GameManager.ui_font
	t.default_font_size = 15
	theme = t


func _make_btn(text: String, bg: Color, text_col: Color = OUTLINE) -> Button:
	var b := Button.new()
	b.text = text
	b.focus_mode = Control.FOCUS_NONE
	b.add_theme_color_override("font_color", text_col)
	b.add_theme_color_override("font_hover_color", text_col)
	b.add_theme_color_override("font_pressed_color", text_col)
	b.add_theme_color_override("font_disabled_color", Color(0.55, 0.52, 0.50))

	var normal := StyleBoxFlat.new()
	normal.bg_color = bg
	normal.border_color = OUTLINE
	normal.set_border_width_all(3)
	normal.set_corner_radius_all(12)
	normal.content_margin_left = 14
	normal.content_margin_right = 14
	normal.content_margin_top = 8
	normal.content_margin_bottom = 8

	var hover := normal.duplicate() as StyleBoxFlat
	hover.bg_color = bg.lightened(0.08)

	var pressed := normal.duplicate() as StyleBoxFlat
	pressed.bg_color = bg.darkened(0.08)

	var disabled := normal.duplicate() as StyleBoxFlat
	disabled.bg_color = Color("#DDD6CC")

	b.add_theme_stylebox_override("normal", normal)
	b.add_theme_stylebox_override("hover", hover)
	b.add_theme_stylebox_override("pressed", pressed)
	b.add_theme_stylebox_override("disabled", disabled)
	return b


func _build_ui() -> void:
	var root_vbox := VBoxContainer.new()
	root_vbox.set_anchors_preset(Control.PRESET_FULL_RECT)
	root_vbox.add_theme_constant_override("separation", 0)
	add_child(root_vbox)

	# --- TOP HUD BAR ---
	var top_bar := PanelContainer.new()
	var top_sb := StyleBoxFlat.new()
	top_sb.bg_color = Color("#FFF3DC")
	top_sb.border_color = OUTLINE
	top_sb.border_width_bottom = 4
	top_sb.content_margin_left = 18
	top_sb.content_margin_right = 18
	top_sb.content_margin_top = 10
	top_sb.content_margin_bottom = 10
	top_bar.add_theme_stylebox_override("panel", top_sb)
	root_vbox.add_child(top_bar)

	var top_hbox := HBoxContainer.new()
	top_hbox.add_theme_constant_override("separation", 12)
	top_bar.add_child(top_hbox)

	lbl_money = Label.new()
	lbl_money.add_theme_font_size_override("font_size", 24)
	lbl_money.add_theme_color_override("font_color", Color("#1B7F5C"))
	top_hbox.add_child(lbl_money)

	lbl_mps = Label.new()
	lbl_mps.add_theme_font_size_override("font_size", 15)
	lbl_mps.add_theme_color_override("font_color", Color("#6D594A"))
	top_hbox.add_child(lbl_mps)

	var spacer1 := Control.new()
	spacer1.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	top_hbox.add_child(spacer1)

	btn_prev_tier = _make_btn("◀", Color("#FFE8C2"))
	btn_prev_tier.pressed.connect(_on_prev_tier)
	top_hbox.add_child(btn_prev_tier)

	lbl_tier = Label.new()
	lbl_tier.add_theme_font_size_override("font_size", 16)
	lbl_tier.add_theme_color_override("font_color", OUTLINE)
	top_hbox.add_child(lbl_tier)

	btn_next_tier = _make_btn("▶", Color("#FFE8C2"))
	btn_next_tier.pressed.connect(_on_next_tier)
	top_hbox.add_child(btn_next_tier)

	var spacer2 := Control.new()
	spacer2.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	top_hbox.add_child(spacer2)

	btn_boss = _make_btn("★ 보스 고물 호출", Color("#F28482"), Color("#FFFDF7"))
	btn_boss.pressed.connect(_on_boss_pressed)
	top_hbox.add_child(btn_boss)

	btn_skill = _make_btn("스킬트리 (Tab)", Color("#FFD166"))
	btn_skill.pressed.connect(_toggle_skill_tree)
	top_hbox.add_child(btn_skill)

	btn_mute = _make_btn("소리 ON", Color("#E8E2D6"))
	btn_mute.pressed.connect(_on_mute_pressed)
	top_hbox.add_child(btn_mute)

	# --- CENTER WORKBENCH CANVAS ---
	workbench = WorkbenchScript.new()
	workbench.size_flags_vertical = Control.SIZE_EXPAND_FILL
	workbench.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	root_vbox.add_child(workbench)

	# --- BOTTOM CONTROL BAR ---
	var bot_bar := PanelContainer.new()
	var bot_sb := StyleBoxFlat.new()
	bot_sb.bg_color = Color("#FFF3DC")
	bot_sb.border_color = OUTLINE
	bot_sb.border_width_top = 4
	bot_sb.content_margin_left = 18
	bot_sb.content_margin_right = 18
	bot_sb.content_margin_top = 10
	bot_sb.content_margin_bottom = 10
	bot_bar.add_theme_stylebox_override("panel", bot_sb)
	root_vbox.add_child(bot_bar)

	var bot_hbox := HBoxContainer.new()
	bot_hbox.add_theme_constant_override("separation", 14)
	bot_bar.add_child(bot_hbox)

	btn_spawn = _make_btn("다음 고물 올리기 (Space)", Color("#81B29A"), Color("#FFFDF7"))
	btn_spawn.pressed.connect(func(): workbench.spawn_scrap(false))
	bot_hbox.add_child(btn_spawn)

	btn_fever = _make_btn("⚡ 오버클럭 피버 (0%)", Color("#FF9F1C"), Color("#FFFDF7"))
	btn_fever.pressed.connect(func(): GameManager.trigger_fever())
	bot_hbox.add_child(btn_fever)

	var spacer3 := Control.new()
	spacer3.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bot_hbox.add_child(spacer3)

	var hint_lbl := Label.new()
	hint_lbl.text = "단축키: Space(고물 투입) · Tab(스킬트리) · F(피버) · F9(디버그 치트)"
	hint_lbl.add_theme_font_size_override("font_size", 13)
	hint_lbl.add_theme_color_override("font_color", Color("#7A6858"))
	bot_hbox.add_child(hint_lbl)

	# --- SKILL TREE OVERLAY ---
	skill_tree = SkillTreeScript.new()
	skill_tree.set_anchors_preset(Control.PRESET_FULL_RECT)
	skill_tree.closed.connect(_on_skill_tree_closed)
	add_child(skill_tree)

	# --- F9 DEBUG CHEAT PANEL ---
	_build_debug_panel()

	# --- ENDING CELEBRATION MODAL ---
	_build_ending_panel()


func _build_debug_panel() -> void:
	debug_panel = PanelContainer.new()
	debug_panel.visible = false
	debug_panel.position = Vector2(20, 74)
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color("#2B2D42")
	sb.border_color = Color("#FFD166")
	sb.set_border_width_all(3)
	sb.set_corner_radius_all(12)
	sb.content_margin_left = 12
	sb.content_margin_right = 12
	sb.content_margin_top = 10
	sb.content_margin_bottom = 10
	debug_panel.add_theme_stylebox_override("panel", sb)
	add_child(debug_panel)

	var vb := VBoxContainer.new()
	vb.add_theme_constant_override("separation", 6)
	debug_panel.add_child(vb)

	var title := Label.new()
	title.text = "[F9] 밸런스 테스트 치트"
	title.add_theme_color_override("font_color", Color("#FFD166"))
	vb.add_child(title)

	var b1 := _make_btn("+$500 추가", Color("#52B788"), Color("#FFFDF7"))
	b1.pressed.connect(func(): GameManager.add_money(500))
	vb.add_child(b1)

	var b2 := _make_btn("+$25,000 추가", Color("#4EA8DE"), Color("#FFFDF7"))
	b2.pressed.connect(func(): GameManager.add_money(25000))
	vb.add_child(b2)

	var b3 := _make_btn("피버 100% 충전", Color("#FF9F1C"), Color("#FFFDF7"))
	b3.pressed.connect(func():
		GameManager.fever_charge = 100.0
		_refresh_hud()
	)
	vb.add_child(b3)

	var b4 := _make_btn("세이브 초기화", Color("#EF476F"), Color("#FFFDF7"))
	b4.pressed.connect(func():
		GameManager.reset_save()
		workbench.active_scrap.clear()
		workbench.parts.clear()
		workbench.spawn_scrap(false)
		_refresh_hud()
	)
	vb.add_child(b4)


func _build_ending_panel() -> void:
	ending_panel = PanelContainer.new()
	ending_panel.visible = false
	ending_panel.set_anchors_preset(Control.PRESET_CENTER)
	ending_panel.custom_minimum_size = Vector2(520, 320)
	ending_panel.offset_left = -260
	ending_panel.offset_right = 260
	ending_panel.offset_top = -160
	ending_panel.offset_bottom = 160

	var sb := StyleBoxFlat.new()
	sb.bg_color = Color("#FFF8EC")
	sb.border_color = Color("#FFB703")
	sb.set_border_width_all(5)
	sb.set_corner_radius_all(20)
	sb.content_margin_left = 28
	sb.content_margin_right = 28
	sb.content_margin_top = 24
	sb.content_margin_bottom = 24
	ending_panel.add_theme_stylebox_override("panel", sb)
	add_child(ending_panel)

	var vb := VBoxContainer.new()
	vb.add_theme_constant_override("separation", 14)
	ending_panel.add_child(vb)

	var h := Label.new()
	h.text = "★ 공방 마스터 달성! (프로토타입 엔딩) ★"
	h.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	h.add_theme_font_size_override("font_size", 22)
	h.add_theme_color_override("font_color", Color("#D97706"))
	vb.add_child(h)

	lbl_ending_stats = Label.new()
	lbl_ending_stats.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl_ending_stats.add_theme_font_size_override("font_size", 16)
	lbl_ending_stats.add_theme_color_override("font_color", OUTLINE)
	vb.add_child(lbl_ending_stats)

	var close_btn := _make_btn("무한 모드로 계속 플레이하기!", Color("#52B788"), Color("#FFFDF7"))
	close_btn.pressed.connect(func():
		ending_panel.visible = false
		workbench.input_blocked = false
	)
	vb.add_child(close_btn)


func _process(_delta: float) -> void:
	_refresh_hud()


func _refresh_hud() -> void:
	lbl_money.text = GameManager.format_money(GameManager.money)
	lbl_mps.text = "(%s /초)" % GameManager.format_money(GameManager.money_per_sec)
	lbl_tier.text = GameManager.get_tier_name(GameManager.current_tier)

	btn_prev_tier.disabled = GameManager.current_tier <= 1
	btn_next_tier.disabled = GameManager.current_tier >= GameManager.unlocked_tier

	if GameManager.can_afford_any_skill():
		btn_skill.text = "★ 스킬트리 (구매 가능!)"
	else:
		btn_skill.text = "스킬트리 (Tab)"

	if GameManager.is_fever_active():
		btn_fever.text = "⚡ 피버 폭주 중! (%.1f초)" % GameManager.fever_time_left
		btn_fever.disabled = true
	elif GameManager.fever_charge >= 100.0:
		btn_fever.text = "⚡ 오버클럭! 고철 폭주 (F)"
		btn_fever.disabled = false
	else:
		btn_fever.text = "⚡ 피버 충전 중 (%d%%)" % int(GameManager.fever_charge)
		btn_fever.disabled = true

	btn_spawn.visible = not GameManager.has_auto_conveyor()


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey:
		var ke := event as InputEventKey
		if not ke.pressed or ke.echo:
			return
		if ke.keycode == KEY_TAB or ke.keycode == KEY_ESCAPE:
			if ke.keycode == KEY_ESCAPE and not skill_tree.visible:
				return
			_toggle_skill_tree()
		elif ke.keycode == KEY_SPACE and not skill_tree.visible:
			workbench.spawn_scrap(false)
		elif ke.keycode == KEY_F and not skill_tree.visible:
			GameManager.trigger_fever()
		elif ke.keycode == KEY_F9:
			debug_panel.visible = not debug_panel.visible


func _toggle_skill_tree() -> void:
	if skill_tree.visible:
		skill_tree.close_tree()
	else:
		workbench.input_blocked = true
		skill_tree.open_tree()


func _on_skill_tree_closed() -> void:
	workbench.input_blocked = false


func _on_boss_pressed() -> void:
	if skill_tree.visible:
		skill_tree.close_tree()
	workbench.spawn_scrap(true)


func _on_prev_tier() -> void:
	if GameManager.current_tier > 1:
		GameManager.current_tier -= 1
		workbench.active_scrap.clear()
		workbench.spawn_scrap(false)
		_refresh_hud()


func _on_next_tier() -> void:
	if GameManager.current_tier < GameManager.unlocked_tier:
		GameManager.current_tier += 1
		workbench.active_scrap.clear()
		workbench.spawn_scrap(false)
		_refresh_hud()


func _on_mute_pressed() -> void:
	SfxManager.muted = not SfxManager.muted
	btn_mute.text = "소리 OFF" if SfxManager.muted else "소리 ON"


func _show_ending_card() -> void:
	var mins := int(GameManager.play_time_sec / 60.0)
	var secs := int(fmod(GameManager.play_time_sec, 60.0))
	lbl_ending_stats.text = (
		"추락한 외계 모선을 완전히 분해·제련했습니다!\n\n"
		+ "총 플레이 시간: %d분 %02d초\n" % [mins, secs]
		+ "총 푼 나사 수: %d개\n" % GameManager.total_screws_removed
		+ "총 분해한 고물: %d대\n" % GameManager.total_scraps_dismantled
		+ "총 주조한 주괴: %d개\n" % GameManager.total_ingots_smelted
		+ "누적 매출: %s" % GameManager.format_money(GameManager.total_earned)
	)
	ending_panel.visible = true
	workbench.input_blocked = true
