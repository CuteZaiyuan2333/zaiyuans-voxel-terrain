@tool
extends EditorPlugin

enum MODE {
	insert,
	comment
}

var api_key: String = ""
var url: String = ""
var model: String = ""
var req: HTTPRequest
var max_tokens: int = 100
var input_line: int = 0
var awaiting_code: bool = false
var awaiting_comments: bool = false
var input_editor: CodeEdit = null
var current_editor: CodeEdit = null
var reasoning: String = ""
var instructions: String = ""
var popup: EditorContextMenuPlugin
const files: Dictionary[String,String] = {
	"settings": "res://addons/Godex/files/settings.cfg",
	"instructions": "res://addons/Godex/files/instructions.md"
}

func _enable_plugin() -> void:
	var file := ConfigFile.new()
	var error := file.load(files.settings)
	if error != OK:
		print(error_string(file.get_open_error()))
		return
	api_key = file.get_value("MAIN","api_key")
	url = file.get_value("MAIN","url")
	model = file.get_value("MAIN","model")
	max_tokens = file.get_value("MAIN","max_tokens")
	reasoning = file.get_value("MAIN","reasoning")
	instructions = FileAccess.get_file_as_string(files.instructions)
	_display_ascii()
	_test_connection()
	
func _test_connection() -> void:
	if api_key.is_empty():
		display_main("API key not set. Please add it in settings.cfg")
		return
	if url.is_empty():
		display_main("URL not set. Please add it in settings.cfg")
		return
	if model.is_empty():
		display_main("Model not set. Please add it in settings.cfg")
		return
	var temp_req := HTTPRequest.new()
	add_child(temp_req)
	var message: Dictionary = {
		"input": "ping",
		"model": model,
		"max_output_tokens": 16,
		"text": {
			"format": {
				"type": "json_schema",
				"name": "code_and_text",
				"schema": {
					"type": "object",
					"additionalProperties": false,
					"properties": {
						"code": {"type": "string"},
						"text": {"type": "string"}
					},
					"required": ["code","text"]
				}
			}
		}
	}
	var body: String = JSON.new().stringify(message)
	var headers: PackedStringArray = [
		"Content-Type: application/json",
		"Authorization: Bearer %s" % api_key
	]
	var start_ms: int = Time.get_ticks_msec()
	var err := temp_req.request(url, headers, HTTPClient.METHOD_POST, body)
	if err != OK:
		display_main("Request error: %s" % error_string(err))
		temp_req.queue_free()
		return
	var result: Array = await temp_req.request_completed
	var response_code: int = result[1]
	var raw: String = result[3].get_string_from_utf8()
	var elapsed_ms: int = Time.get_ticks_msec() - start_ms
	display_main("Connection test complete")
	display_extra("status code: %d\n\tlatency: %d ms\n\tbytes: %d" % [
		response_code, elapsed_ms, raw.length()])
	temp_req.queue_free()

func _display_ascii():
	display_main("""[color=orange][b]   ██████╗  ██████╗ ██████╗ ███████╗██╗  ██╗
  ██╔════╝ ██╔═══██╗██╔══██╗██╔════╝╚██╗██╔╝
  ██║  ███╗██║   ██║██║  ██║█████╗   ╚███╔╝ 
  ██║   ██║██║   ██║██║  ██║██╔══╝   ██╔██╗ 
  ╚██████╔╝╚██████╔╝██████╔╝███████╗██╔╝ ██╗
   ╚═════╝  ╚═════╝ ╚═════╝ ╚══════╝╚═╝  ╚═╝[/b][/color]""")

func _enter_tree() -> void:
	popup = PopupExtra.new()
	popup.connect("analyze",_analyze_selection)
	add_context_menu_plugin(EditorContextMenuPlugin.CONTEXT_SLOT_SCRIPT_EDITOR_CODE,popup)

func _ready() -> void:
	req = HTTPRequest.new()
	add_child(req)
	req.connect("request_completed",_req_completed)
	
func _exit_tree() -> void:
	remove_context_menu_plugin(popup)
	req.queue_free()

func display_extra(text: String):
	print_rich("\n[i]➤\t%s\n[/i]" % text)
	
func display_main(text: String):
	print_rich("\n%s\n\n" % text)

func _req_completed(result, response_code, headers, body):
	var raw = body.get_string_from_utf8()
	if response_code < 200 or response_code >= 300:
		print("error: %d\n%s" % [response_code, raw])
		return
	
	var json := JSON.new()
	var parsed = json.parse_string(raw)
	if typeof(parsed) != TYPE_DICTIONARY:
		print("invalid return data %s" % str(parsed))
		return
	
	process_output(parsed)
	
func process_output(parsed: Dictionary):
	var status: String = parsed["status"]
	var error: String = str(parsed["error"])
	var output = parsed["output"]
	var output_string: String = ""
	for item in output:
		if typeof(item) == TYPE_DICTIONARY and item.get("type") == "message":
			for c in item.get("content", []):
				if typeof(c) == TYPE_DICTIONARY and c.get("type") == "output_text":
					output_string = str(c.get("text", ""))
					break
	var input_tokens: int = parsed["usage"]["input_tokens"]
	var output_tokens: int = parsed["usage"]["output_tokens"]
	var extra_string: String = "status: %s\n\terror: %s\n\tinput tokens: %d\n\toutput tokens: %d" % [
		status, error, input_tokens, output_tokens]
	display_extra(extra_string)
	
	input_editor.editable = true
	input_editor.deselect()
	input_editor.call_deferred("grab_click_focus")
	
	if status == "incomplete":
		display_main(str(parsed["incomplete_details"]))
		display_main("Failed")
		awaiting_code = false
		awaiting_comments = false
		return
	
	var parsed_output: Dictionary = JSON.parse_string(output_string)
	if parsed_output.has("text"):
		display_main(parsed_output["text"])
	if parsed_output.has("code"):
		if awaiting_code:
			awaiting_code = false
			input_editor.set_line(input_line, parsed_output["code"])
		if awaiting_comments:
			awaiting_comments = false
			apply_comments(input_line, parsed_output["code"])
			
func apply_comments(line: int, comments_str: String):
	var comments: Array = JSON.parse_string(comments_str)
	input_editor.begin_complex_operation()
	for n in comments.size():
		input_editor.set_line(line+n , input_editor.get_line(line+n) + " " + comments[n])
	input_editor.end_complex_operation()

func send_message(text: String, mode: MODE):
	input_editor.editable = false
	input_editor.caret_draw_when_editable_disabled = true
	var text_package = "MODE:\n%s\n\nINFO:\n%s\n\nSCRIPT:\n%s" % [
		str(MODE.keys()[mode]), text, EditorInterface.get_script_editor().get_current_script().source_code]
	var message: Dictionary = {
		"input": text_package,
		"model": model,
		"max_output_tokens": max_tokens,
		"text": {
			"format": {
				"type": "json_schema",
				"name": "code_and_text",
				"schema": {
					"type": "object",
					"additionalProperties": false,
					"properties": {
						"code": {"type": "string"},
						"text": {"type": "string"}
					},
					"required": ["code","text"]
				}
			}
		},
		"tools": [
			{"type": "web_search"},
			{"type": "shell"},
			{"type": "web_search_preview"}],
		"parallel_tool_calls": true,
		"tool_choice": "auto",
		"reasoning": {"effort": reasoning},
		"instructions": instructions
	}
	var body = JSON.new().stringify(message)
	var headers: PackedStringArray = ["Content-Type: application/json", "Authorization: Bearer %s" % api_key]
	var error := req.request(url,headers,HTTPClient.METHOD_POST,body)
	if error != OK:
		display_main("error sending request: %s" % error_string(error))
		
func _process(delta: float) -> void:
	var main_edit := EditorInterface.get_script_editor().get_current_editor()
	if main_edit: current_editor = main_edit.get_base_editor()
	if not current_editor: return
	var current_line: int = current_editor.get_caret_line()
	var current_string: String = current_editor.get_line(current_line).strip_edges()
	if current_string.begins_with("#/") and current_string.ends_with("/#"):
		if not awaiting_code: request_insert(current_line,current_string)

func can_process() -> bool:
	if awaiting_code or awaiting_comments:
		display_main("[b]You are still processing an input.[/b]")
		return false
	else: return true
	
func request_insert(line: int, line_string: String):
	if not can_process(): return
	awaiting_code = true
	input_line = line
	input_editor = current_editor
	var input_string: String = line_string.lstrip("#/")
	input_string = input_string.rstrip("/#")
	send_message(input_string, MODE.insert)
	display_extra(input_string)
	start_loading_animation(input_line, input_editor)

func _analyze_selection():
	if not can_process(): return
	awaiting_comments = true
	if not current_editor: return
	if not current_editor.has_selection():
		display_extra("No selection found.")
		return
	var selected_code: String = current_editor.get_selected_text()
	input_line = min(current_editor.get_selection_origin_line(), current_editor.get_caret_line())
	var end_line = max(current_editor.get_selection_origin_line(), current_editor.get_caret_line())
	input_editor = current_editor
	for line: int in range(input_line, end_line+1):
		input_editor.set_line_background_color(line, Color(1.0, 0.6, 0.1, 0.05))
	send_message(selected_code, MODE.comment)
	display_extra("Analyzing code from line %d to %d" % [input_line+1, end_line+1])
	
func start_loading_animation(line: int, editor: CodeEdit, frame: int = 0) -> void:
	if not editor or not awaiting_code: return
	const speed: float = 0.1
	var loading_spinner: String = "⣾⣽⣻⢿⡿⣟⣯⣷"
	if frame >= loading_spinner.length(): frame = 0
	var new_line: String = "# %s loading %s #" % [loading_spinner[frame],loading_spinner[frame]]
	editor.set_line(line, new_line)
	editor.set_line_background_color(line, Color(1.0, 0.6, 0.1, 0.05))
	await get_tree().create_timer(speed).timeout
	start_loading_animation(line, editor, frame + 1)

class PopupExtra:
	extends EditorContextMenuPlugin
	
	signal analyze
	
	func _popup_menu(paths: PackedStringArray) -> void:
		var current_editor: CodeEdit
		var main_edit := EditorInterface.get_script_editor().get_current_editor()
		if main_edit: current_editor = main_edit.get_base_editor()
		if not current_editor: return
		if not current_editor.has_selection(): return
		add_context_menu_item("Analyze with Godex",emit_analyze)
	
	func emit_analyze(_arg):
		analyze.emit()
