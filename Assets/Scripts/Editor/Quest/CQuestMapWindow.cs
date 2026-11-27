using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CQuestMapWindow : EditorWindow
{
    private class CPlacement
    {
        public string scenePath;
        public string label;
        public string npcId;
        public CTalkNpc npc;
        public string key;
        public Object normal;
        public readonly List<CTalkScriptSO> talks = new List<CTalkScriptSO>();
        public readonly List<CQuestSO> quests = new List<CQuestSO>();
    }

    private readonly List<CPlacement> _placements = new List<CPlacement>();
    private readonly List<string> _maps = new List<string>();
    private readonly List<CQuestSO> _quests = new List<CQuestSO>();
    private int _map;
    private int _person;
    private string _character = "";
    private bool _unassigned;
    private string _search = "";
    private string _newName = "";
    private string _newId = "";
    private string _newTitle = "";
    private Vector3 _position;
    private CQuestSO _quest;
    private CTalkScriptSO _talk;
    private Object _sharedEdit;
    private bool _showConditions;
    private bool _showTravelSettings;
    private int _place;
    private CTravelGuideSO _currentGuide;
    private Editor _guideEditor;
    private Vector2 _scroll;
    private int _tab;
    private bool _refresh = true;
    private bool _pickGround;
    private string _groundScene;
    private CTalkScriptSO _previewTalk;
    private string _previewNode;
    private int _previewLine;
    private int _previewStage;

    [MenuItem("Espoir/퀘스트와 NPC 편집")]
    private static void Open()
    {
        GetWindow<CQuestMapWindow>("퀘스트와 NPC");
    }

    private void OnEnable()
    {
        EditorApplication.hierarchyChanged += Repaint;
        Undo.undoRedoPerformed += RequestRefresh;
        SceneView.duringSceneGui += PickGround;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= Repaint;
        Undo.undoRedoPerformed -= RequestRefresh;
        SceneView.duringSceneGui -= PickGround;
        DestroyImmediate(_guideEditor);
    }

    private void RequestRefresh()
    {
        _refresh = true;
        Repaint();
    }

    private void RefreshIndex()
    {
        _refresh = false;
        string oldMap = _map > 0 && _map <= _maps.Count ? _maps[_map - 1] : null;
        List<CPlacement> oldRows = FilterRows(oldMap);
        string key = _person > 0 && _person <= oldRows.Count ? oldRows[_person - 1].key : null;
        _maps.Clear();
        _placements.Clear();
        _quests.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:CQuestSO", new[] { CQuestEditorUtility.QUEST_FOLDER }))
        {
            _quests.Add(AssetDatabase.LoadAssetAtPath<CQuestSO>(AssetDatabase.GUIDToAssetPath(guid)));
        }
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (entry.enabled && File.Exists(entry.path) && !_maps.Contains(entry.path)) _maps.Add(entry.path);
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!string.IsNullOrEmpty(scene.path) && !_maps.Contains(scene.path)) _maps.Add(scene.path);
        }
        foreach (string path in _maps)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasInHierarchy = scene.IsValid();
            bool preview = !wasInHierarchy || !scene.isLoaded;
            if (preview) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (CTalkNpc npc in root.GetComponentsInChildren<CTalkNpc>(true))
                    {
                        CPlacement row = new CPlacement();
                        row.scenePath = path;
                        row.key = GlobalObjectId.GetGlobalObjectIdSlow(npc).ToString();
                        row.npcId = npc.NpcId;
                        row.label = npc.DisplayName + " (" + npc.name + ") · " + Path.GetFileNameWithoutExtension(path);
                        row.npc = preview ? null : npc;
                        SerializedObject data = new SerializedObject(npc);
                        row.normal = npc.TravelGuide != null ? (Object)npc.TravelGuide : data.FindProperty("_script").objectReferenceValue;
                        if (npc.TravelGuide == null) AddQuest(row, data.FindProperty("_script"));
                        SerializedProperty list = data.FindProperty("_questTalks");
                        for (int j = 0; j < list.arraySize; j++) AddQuest(row, list.GetArrayElementAtIndex(j));
                        _placements.Add(row);
                    }
                }
            }
            finally
            {
                if (preview) EditorSceneManager.CloseScene(scene, !wasInHierarchy);
            }
        }
        _map = _maps.IndexOf(oldMap) + 1;
        _person = FilterRows(oldMap).FindIndex(row => row.key == key) + 1;
        if (key != null && _person == 0) ResetSelection();
    }

    private List<CPlacement> FilterRows(string map)
    {
        return _placements.FindAll(row => (map == null || row.scenePath == map) &&
            (string.IsNullOrEmpty(_character) || row.npcId == _character));
    }

    private void OpenPlacement(CPlacement row)
    {
        if (row.npc == null)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(row.scenePath);
            RefreshIndex();
            row = _placements.Find(p => p.key == row.key);
            if (row == null) return;
        }
        _map = _maps.IndexOf(row.scenePath) + 1;
        _character = row.npcId;
        _person = FilterRows(row.scenePath).IndexOf(row) + 1;
        _sharedEdit = null;
        _talk = row.talks.Find(t => t.QuestLink.quest == _quest);
        if (_talk != null && row.normal == _talk) _sharedEdit = _talk;
        _tab = 1;
        _scroll = Vector2.zero;
    }

    private static void AddQuest(CPlacement row, SerializedProperty slot)
    {
        CTalkScriptSO talk = slot.objectReferenceValue as CTalkScriptSO;
        if (talk != null && !row.talks.Contains(talk)) row.talks.Add(talk);
        CQuestSO quest = talk != null ? talk.QuestLink.quest : null;
        if (quest != null && !row.quests.Contains(quest)) row.quests.Add(quest);
    }

    private void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { GUILayout.Label("Play 종료 후 편집하세요."); return; }
        if (_refresh) RefreshIndex();
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("맵과 인물 찾기", EditorStyles.boldLabel);
            if (GUILayout.Button("목록 새로고침", GUILayout.Width(104))) RefreshIndex();
        }
        List<string> names = new List<string> { "전체 맵" };
        foreach (string path in _maps)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (_maps.Exists(other => other != path && Path.GetFileNameWithoutExtension(other) == name))
                name = Path.ChangeExtension(path, null);
            names.Add(name);
        }
        int map = EditorGUILayout.Popup("의뢰를 주는 맵", _map, names.ToArray());
        if (map != _map) { _pickGround = false; _map = map; ResetSelection(); }
        List<CPlacement> rows = _placements.FindAll(p => _map == 0 || p.scenePath == _maps[_map - 1]);
        List<string> ids = new List<string> { "" };
        names = new List<string> { "전체 인물" };
        foreach (CPlacement row in rows)
        {
            if (string.IsNullOrEmpty(row.npcId) || ids.Contains(row.npcId)) continue;
            ids.Add(row.npcId);
            names.Add(row.label.Split('(')[0] + " · " + row.npcId);
        }
        int character = EditorGUILayout.Popup("인물", Mathf.Max(0, ids.IndexOf(_character)), names.ToArray());
        if (_character != ids[character])
        {
            ResetSelection();
            _character = ids[character];
        }
        if (_character != "") rows.RemoveAll(row => row.npcId != _character);
        names = new List<string> { "전체 배치" };
        foreach (CPlacement row in rows) names.Add(row.label);
        int person = EditorGUILayout.Popup("편집할 NPC", _person, names.ToArray());
        if (person != _person) { ResetSelection(); _person = person; }
        CPlacement current = _person > 0 ? rows[_person - 1] : null;
        EditorGUILayout.Space();
        _tab = GUILayout.Toolbar(_tab, new[] { "전체 의뢰", "NPC 대사와 의뢰", "NPC 배치" });
        if (_tab != 2) _pickGround = false;
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        if (_tab == 0) DrawCatalog(rows, current);
        else if (_tab == 2) DrawPlacement();
        else if (current == null) GUILayout.Label("위의 ‘편집할 NPC’에서 인물을 선택하세요.");
        else if (current.npc == null)
        {
            if (GUILayout.Button("이 배치의 씬 열기") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(current.scenePath);
                RequestRefresh();
            }
        }
        else DrawNpc(current.npc);
        EditorGUILayout.EndScrollView();
    }

    private void ResetSelection()
    {
        _person = 0;
        _quest = null;
        _talk = null;
        _sharedEdit = null;
    }

    private void DrawCatalog(List<CPlacement> rows, CPlacement selected)
    {
        _search = EditorGUILayout.TextField("퀘스트 이름 검색", _search);
        _unassigned = EditorGUILayout.Toggle("미배정만 보기", _unassigned);
        bool selectionVisible = false;
        foreach (CQuestSO quest in _quests)
        {
            if (quest == null || !(quest.Title ?? "").Contains(_search)) continue;
            bool assigned = _placements.Exists(p => p.quests.Contains(quest));
            if (_unassigned && assigned) continue;
            if (selected != null && !selected.quests.Contains(quest)) continue;
            if ((_map > 0 || _character != "") && !rows.Exists(p => p.quests.Contains(quest))) continue;
            if (GUILayout.Button(string.IsNullOrEmpty(quest.Title) ? quest.name : quest.Title)) _quest = quest;
            if (quest == _quest) selectionVisible = true;
        }
        if (!selectionVisible) _quest = null;
        if (GUILayout.Button("미배정 퀘스트 만들기"))
        {
            _map = 0;
            _character = "";
            ResetSelection();
            _search = "";
            _unassigned = true;
            _quest = CQuestEditorUtility.CreateQuest("Quest");
            _quests.Add(_quest);
        }
        if (_quest == null) return;
        foreach (CPlacement row in _placements)
            if (row.quests.Contains(_quest) && GUILayout.Button("이 NPC 편집 · " + row.label))
            {
                OpenPlacement(row);
                GUIUtility.ExitGUI();
            }
        if (!_placements.Exists(p => p.quests.Contains(_quest))) GUILayout.Label("조회한 씬에 배정된 NPC 없음");
        foreach (CQuestSO other in _quests)
            if (other != null && other.Prerequisite == _quest) GUILayout.Label("이 의뢰를 깨면 열림 · " + other.Title);
        DrawQuest(_quest);
    }

    private void DrawQuest(CQuestSO quest)
    {
        SerializedObject data = new SerializedObject(quest);
        EditorGUILayout.PropertyField(data.FindProperty("_title"), new GUIContent("의뢰 제목"));
        SerializedProperty objectives = data.FindProperty("_objectives");
        EditorGUILayout.PropertyField(objectives, new GUIContent("목표 목록"), true);
        data.ApplyModifiedProperties();
        for (int i = 0; i < objectives.arraySize; i++)
        {
            SerializedProperty goal = objectives.GetArrayElementAtIndex(i);
            if (goal.FindPropertyRelative("kind").intValue != (int)EQuestObjectiveKind.Talk) continue;
            if (GUILayout.Button("목표 " + (i + 1) + " · 대화할 NPC를 이름으로 선택"))
            {
                GenericMenu menu = new GenericMenu();
                foreach (CPlacement row in _placements)
                {
                    if (string.IsNullOrEmpty(row.npcId)) continue;
                    string path = goal.propertyPath;
                    string label = Path.GetFileNameWithoutExtension(row.scenePath) + "/" + row.label;
                    menu.AddItem(new GUIContent(label + "/대화 대상"), false,
                        () => SetTarget(quest, path, row.npcId, false));
                    menu.AddItem(new GUIContent(label + "/대화 대상과 길 안내 NPC"), false,
                        () => SetTarget(quest, path, row.npcId, true));
                }
                menu.ShowAsContext();
            }
        }
        EditorGUILayout.PropertyField(data.FindProperty("_reportText"), new GUIContent("보고할 때 안내 문구"));
        EditorGUILayout.PropertyField(data.FindProperty("_reportNpcId"), new GUIContent("보고할 NPC ID"));
        _showConditions = EditorGUILayout.Foldout(_showConditions, "수락 조건과 완료 보상", true);
        if (_showConditions)
        {
            EditorGUILayout.PropertyField(data.FindProperty("_prerequisite"), new GUIContent("먼저 완료할 의뢰"));
            EditorGUILayout.PropertyField(data.FindProperty("_requiredBosses"), new GUIContent("먼저 처치할 보스"));
            EditorGUILayout.PropertyField(data.FindProperty("_bossClearReward"), new GUIContent("완료 시 처치로 기록할 보스"));
        }
        data.ApplyModifiedProperties();
        foreach (string problem in CQuestEditorUtility.GetProblems(quest))
            EditorGUILayout.HelpBox(problem, MessageType.Warning);
    }

    private static void SetTarget(CQuestSO quest, string path, string id, bool waypoint)
    {
        SerializedObject data = new SerializedObject(quest);
        SerializedProperty goal = data.FindProperty(path);
        if (goal == null) return;
        goal.FindPropertyRelative("npcId").stringValue = id;
        if (waypoint)
        {
            goal.FindPropertyRelative("useDestination").boolValue = true;
            goal.FindPropertyRelative("destinationNpcId").stringValue = id;
        }
        data.ApplyModifiedProperties();
    }

    private void DrawTravel(CTravelGuideSO guide)
    {
        if (_currentGuide != guide)
        {
            _currentGuide = guide;
            _place = 0;
            _showTravelSettings = false;
        }
        SerializedObject data = new SerializedObject(guide);
        SerializedProperty places = data.FindProperty("_places");
        if (places.arraySize > 0)
        {
            string[] names = new string[places.arraySize];
            for (int i = 0; i < names.Length; i++)
            {
                SerializedProperty place = places.GetArrayElementAtIndex(i);
                string name = place.FindPropertyRelative("placeName").stringValue;
                string key = place.FindPropertyRelative("mapKey").stringValue;
                names[i] = string.IsNullOrEmpty(name) ? key : name + " (" + key + ")";
            }
            _place = EditorGUILayout.Popup("인사를 바꿀 장소", Mathf.Clamp(_place, 0, names.Length - 1), names);
            SerializedProperty hello = places.GetArrayElementAtIndex(_place).FindPropertyRelative("hello");
            for (int i = 0; i < hello.arraySize; i++)
                EditorGUILayout.PropertyField(hello.GetArrayElementAtIndex(i), new GUIContent("인사 " + (i + 1)));
            if (GUILayout.Button("인사 한 줄 추가"))
            {
                hello.arraySize++;
                hello.GetArrayElementAtIndex(hello.arraySize - 1).stringValue = "";
            }
            data.ApplyModifiedProperties();
        }
        _showTravelSettings = EditorGUILayout.Foldout(_showTravelSettings, "여행 선택지와 장소 전체 설정", true);
        if (_showTravelSettings)
        {
            Editor.CreateCachedEditor(guide, null, ref _guideEditor);
            _guideEditor.OnInspectorGUI();
        }
    }

    private void DrawNpc(CTalkNpc npc)
    {
        SerializedObject data = new SerializedObject(npc);
        SerializedProperty normal = data.FindProperty("_script");
        SerializedProperty guide = data.FindProperty("_travelGuide");
        bool travel = guide.objectReferenceValue != null;
        SerializedProperty slot = travel ? guide : normal;
        Object asset = slot.objectReferenceValue;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("이 배치의 이름표", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(data.FindProperty("_displayName"), new GUIContent("표시 이름"));
        EditorGUILayout.PropertyField(data.FindProperty("_title"), new GUIContent("타이틀 (선택)"));
        EditorGUILayout.LabelField("퀘스트에서 쓰는 NPC ID", npc.NpcId);
        EditorGUILayout.Space();
        GUILayout.Label("일반 대화 · " + npc.DisplayName, EditorStyles.boldLabel);
        if (npc.NpcId == "bune" || npc.ShopUI != null)
            EditorGUILayout.PropertyField(data.FindProperty("_shopUI"), new GUIContent("이 씬의 상점 UI"));
        if (asset != null)
        {
            DrawUses(asset);
            EditorGUILayout.ObjectField("사용 중인 에셋", asset, asset.GetType(), false);
            if (GUILayout.Button("이 NPC만 수정 · 복사본 만들기"))
            {
                string path = AssetDatabase.GetAssetPath(asset);
                string copy = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, null) + "_Local.asset");
                if (AssetDatabase.CopyAsset(path, copy))
                {
                    slot.objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(copy);
                    _sharedEdit = slot.objectReferenceValue;
                    asset = _sharedEdit;
                }
            }
            if (GUILayout.Button("원본 수정 · 함께 쓰는 NPC에 적용")) _sharedEdit = asset;
            if (_sharedEdit == asset && travel)
            {
                DrawTravel((CTravelGuideSO)asset);
            }
            else if (_sharedEdit == asset)
            {
                DrawPreview((CTalkScriptSO)asset);
                CQuestTalkFields.Draw((CTalkScriptSO)asset);
            }
        }
        else if (GUILayout.Button("기본 인사 만들기")) normal.objectReferenceValue = NewTalk(null);
        GUILayout.Space(10);
        GUILayout.Label("이 배치의 의뢰 · 위에서 아래 순서", EditorStyles.boldLabel);
        SerializedProperty list = data.FindProperty("_questTalks");
        EditorGUILayout.PropertyField(list, new GUIContent("의뢰 순서"), true);
        CQuestSO existing = EditorGUILayout.ObjectField("기존 의뢰 추가", null, typeof(CQuestSO), false) as CQuestSO;
        bool create = GUILayout.Button("새 의뢰와 수락·보고 대사 만들기");
        if (existing != null || create)
        {
            CQuestSO quest = existing != null ? existing : CQuestEditorUtility.CreateQuest("Quest");
            if (existing == null)
            {
                SerializedObject created = new SerializedObject(quest);
                created.FindProperty("_reportNpcId").stringValue = npc.NpcId;
                created.ApplyModifiedProperties();
            }
            if (!_quests.Contains(quest)) _quests.Add(quest);
            _talk = NewTalk(quest);
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = _talk;
        }
        if (data.ApplyModifiedProperties())
        {
            EditorSceneManager.MarkSceneDirty(npc.gameObject.scene);
            CPlacement row = _placements.Find(p => p.npc == npc);
            row.label = npc.DisplayName + " (" + npc.name + ") · " + Path.GetFileNameWithoutExtension(row.scenePath);
            row.normal = slot.objectReferenceValue;
            row.talks.Clear();
            row.quests.Clear();
            if (!travel) AddQuest(row, normal);
            for (int j = 0; j < list.arraySize; j++) AddQuest(row, list.GetArrayElementAtIndex(j));
        }
        for (int i = 0; i < list.arraySize; i++)
        {
            CTalkScriptSO talk = list.GetArrayElementAtIndex(i).objectReferenceValue as CTalkScriptSO;
            if (talk == null) continue;
            if (GUILayout.Button("의뢰 편집 · " + (talk.QuestLink.quest != null ? talk.QuestLink.quest.Title : talk.name))) _talk = talk;
        }
        bool linked = false;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            if (_talk == null || entry.objectReferenceValue != _talk) continue;
            linked = true;
            if (GUILayout.Button("선택한 의뢰 대사를 이 배치 전용으로 복제"))
            {
                string path = AssetDatabase.GetAssetPath(_talk);
                string copy = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, null) + "_Local.asset");
                if (AssetDatabase.CopyAsset(path, copy))
                {
                    _talk = AssetDatabase.LoadAssetAtPath<CTalkScriptSO>(copy);
                    entry.objectReferenceValue = _talk;
                    data.ApplyModifiedProperties();
                    RequestRefresh();
                }
            }
            break;
        }
        if (linked && _talk.QuestLink.quest != null)
        {
            DrawQuest(_talk.QuestLink.quest);
            GUILayout.Label("의뢰 대사 · 연결된 대화 에셋에 적용", EditorStyles.boldLabel);
            DrawUses(_talk);
            DrawPreview(_talk);
            CQuestTalkFields.Draw(_talk);
        }
    }

    private void DrawUses(Object asset)
    {
        List<string> uses = new List<string>();
        foreach (CPlacement row in _placements)
            if (row.normal == asset || row.talks.Contains(asset as CTalkScriptSO)) uses.Add(row.label);
        if (uses.Count > 1) EditorGUILayout.HelpBox("이 대사를 함께 쓰는 배치\n" + string.Join("\n", uses), MessageType.Info);
    }

    private void DrawPreview(CTalkScriptSO talk)
    {
        if (GUILayout.Button(_previewTalk == talk ? "미리보기 닫기" : "대화 미리보기"))
        {
            _previewTalk = _previewTalk == talk ? null : talk;
            _previewStage = 0;
            _previewNode = null;
        }
        if (_previewTalk != talk) return;
        CTalkQuestLink link = talk.QuestLink;
        string start = talk.StartNode;
        if (link.quest != null)
        {
            int stage = GUILayout.Toolbar(_previewStage, new[] { "수락 전", "진행 중", "보고 가능", "완료 후" });
            if (stage != _previewStage) _previewNode = null;
            _previewStage = stage;
            string[] starts = { link.offerNode, link.progressNode, link.reportNode, link.doneNode };
            if (!string.IsNullOrEmpty(starts[stage])) start = starts[stage];
        }
        bool restart = GUILayout.Button("처음부터 보기");
        if (_previewNode == null || restart)
        {
            _previewNode = start;
            _previewLine = 0;
        }
        CTalkNode node = talk.Find(_previewNode);
        if (node == null)
        {
            EditorGUILayout.HelpBox("연결된 다음 대사가 없습니다.", MessageType.Info);
            return;
        }
        _previewLine = Mathf.Clamp(_previewLine, 0, node.LineCount - 1);
        EditorGUILayout.HelpBox(node.GetLine(_previewLine), MessageType.None);
        if (_previewLine + 1 < node.LineCount)
        {
            if (GUILayout.Button("다음 문장")) _previewLine++;
        }
        else if (node.HasChoices)
        {
            foreach (CTalkChoice choice in node.choices)
                if (GUILayout.Button(choice.text)) { _previewNode = choice.to ?? ""; _previewLine = 0; }
        }
        else if (!string.IsNullOrEmpty(node.next))
        {
            if (GUILayout.Button("다음 대사")) { _previewNode = node.next; _previewLine = 0; }
        }
        else EditorGUILayout.LabelField("마지막 동작", node.end.ToString());
    }

    private void PickGround(SceneView view)
    {
        if (!_pickGround) return;
        Event input = Event.current;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != _groundScene || _tab != 2 || EditorApplication.isPlayingOrWillChangePlaymode ||
            (input.type == EventType.KeyDown && input.keyCode == KeyCode.Escape))
        {
            _pickGround = false;
            return;
        }
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (input.type != EventType.MouseDown || input.button != 0 || input.alt) return;
        Ray ray = HandleUtility.GUIPointToWorldRay(input.mousePosition);
        if (TryGetGround(scene, ray, out Vector3 point))
        {
            _position = point;
            _pickGround = false;
            Repaint();
        }
        else ShowNotification(new GUIContent("선택한 씬의 지면 Collider를 클릭하세요."));
        input.Use();
    }

    private static bool TryGetGround(Scene scene, Ray ray, out Vector3 point)
    {
        PhysicsScene physics = scene.GetPhysicsScene();
        point = Vector3.zero;
        if (!physics.Raycast(ray.origin, ray.direction, out RaycastHit first, Mathf.Infinity,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        if (first.collider.gameObject.scene == scene)
        {
            point = first.point;
            return true;
        }

        if (physics != Physics.defaultPhysicsScene) return false;
        float nearest = Mathf.Infinity;
        foreach (RaycastHit hit in Physics.RaycastAll(ray, Mathf.Infinity,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.gameObject.scene != scene || hit.distance >= nearest) continue;
            point = hit.point;
            nearest = hit.distance;
        }
        return nearest < Mathf.Infinity;
    }

    private static CTalkScriptSO NewTalk(CQuestSO quest)
    {
        CTalkScriptSO talk = CreateInstance<CTalkScriptSO>();
        List<CTalkNode> nodes = new List<CTalkNode>();
        if (quest == null) nodes.Add(new CTalkNode { id = "hello", lines = new[] { "안녕하세요." }, end = ETalkEnd.Close });
        else
        {
            CTalkQuestLink link = talk.QuestLink;
            link.quest = quest;
            link.offerNode = "offer";
            link.progressNode = "progress";
            link.reportNode = "report";
            link.doneNode = "done";
            nodes.Add(Prompt("offer", "의뢰를 맡아 주시겠어요?", "수락하기", "accepted", ETalkQuestAction.Accept));
            nodes.Add(Prompt("report", "의뢰를 마치셨나요?", "보고하기", "done", ETalkQuestAction.Report));
            nodes.Add(Line("accepted", "부탁드려요."));
            nodes.Add(Line("progress", "어떻게 되어 가나요?"));
            nodes.Add(Line("done", "고맙습니다."));
            nodes.Add(Line("bye", "다음에 이야기해요."));
        }
        talk.SetData(nodes[0].id, "", nodes);
        AssetDatabase.CreateAsset(talk, AssetDatabase.GenerateUniqueAssetPath("Assets/Resources/Dialogue/Talk.asset"));
        return talk;
    }

    private static CTalkNode Line(string id, string text)
    {
        return new CTalkNode { id = id, lines = new[] { text }, end = ETalkEnd.Close };
    }

    private static CTalkNode Prompt(string id, string text, string choice, string next, ETalkQuestAction action)
    {
        return new CTalkNode { id = id, ask = text, choices = new[] {
            new CTalkChoice { text = choice, to = next, questAction = action },
            new CTalkChoice { text = "나중에", to = "bye" } } };
    }

    private void DrawPlacement()
    {
        Scene scene = SceneManager.GetActiveScene();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("배치할 씬", Path.GetFileNameWithoutExtension(scene.path), EditorStyles.boldLabel);
        if (_map == 0 || scene.path != _maps[_map - 1])
        {
            GUILayout.Label("위에서 배치할 맵을 고르고 개인 씬을 여세요.");
            if (_map > 0 && GUILayout.Button("선택한 씬 열기") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(_maps[_map - 1]);
            return;
        }
        if (GUILayout.Button(_pickGround ? "위치 선택 취소" : "씬에서 발 위치 찍기"))
        {
            _pickGround = !_pickGround;
            _groundScene = scene.path;
        }
        if (_pickGround) EditorGUILayout.HelpBox("Scene 창의 지면을 클릭하세요. Esc로 취소합니다.", MessageType.Info);
        _position = EditorGUILayout.Vector3Field("발 위치 (월드 좌표)", _position);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("새 캡슐 NPC", EditorStyles.boldLabel);
        _newName = EditorGUILayout.TextField("새 NPC 이름", _newName);
        _newTitle = EditorGUILayout.TextField("타이틀 (선택)", _newTitle);
        _newId = EditorGUILayout.TextField("새 NPC ID", _newId);
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newId) || string.IsNullOrWhiteSpace(_newName)))
        {
            if (GUILayout.Button("캡슐 NPC와 기본 인사 생성")) Place(scene, null);
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("기존 인물 배치", EditorStyles.boldLabel);
        if (GUILayout.Button("부네 배치 · 상점")) Place(scene, "Bune");
        if (GUILayout.Button("바르가스 배치 · 여행")) Place(scene, "Vargas");
    }

    private void Place(Scene scene, string prefabName)
    {
        string id = prefabName == null ? _newId.Trim() : prefabName.ToLowerInvariant();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (CTalkNpc other in root.GetComponentsInChildren<CTalkNpc>(true))
                if (other.NpcId == id) { ShowNotification(new GUIContent("이 씬에 같은 NPC ID가 있습니다.")); return; }
        GameObject obj;
        if (prefabName == null)
        {
            obj = new GameObject(_newName.Trim());
            obj.AddComponent<CTalkNpc>();
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(obj.transform, false);
            body.transform.localPosition = Vector3.up;
        }
        else
        {
            string path = "Assets/Resources/Prefabs/Npc/" + prefabName + ".prefab";
            obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
        }
        obj.transform.position = _position;
        CTalkNpc npc = obj.GetComponent<CTalkNpc>();
        SerializedObject data = new SerializedObject(npc);
        if (prefabName == null)
        {
            data.FindProperty("_npcId").stringValue = id;
            data.FindProperty("_displayName").stringValue = _newName.Trim();
            data.FindProperty("_title").stringValue = _newTitle.Trim();
            data.FindProperty("_script").objectReferenceValue = NewTalk(null);
        }
        if (prefabName == "Bune")
        {
            List<CShopUI> shops = new List<CShopUI>();
            foreach (GameObject root in scene.GetRootGameObjects()) shops.AddRange(root.GetComponentsInChildren<CShopUI>(true));
            if (shops.Count == 1) data.FindProperty("_shopUI").objectReferenceValue = shops[0];
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        if (prefabName != null) PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
        Undo.RegisterCreatedObjectUndo(obj, "NPC 배치");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = obj;
        RefreshIndex();
        _character = "";
        _map = _maps.IndexOf(scene.path) + 1;
        List<CPlacement> rows = _placements.FindAll(p => p.scenePath == scene.path);
        _person = rows.FindIndex(p => p.npc == npc) + 1;
        _tab = 1;
        _talk = null;
        _sharedEdit = null;
    }
}
