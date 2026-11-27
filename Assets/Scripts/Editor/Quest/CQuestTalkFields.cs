using System;
using UnityEditor;
using UnityEngine;

public static class CQuestTalkFields
{
    /// <summary>
    /// 대사 문장과 선택지의 연결을 실제 대화 에셋에서 편집합니다.
    /// </summary>
    public static void Draw(CTalkScriptSO talk)
    {
        SerializedObject data = new SerializedObject(talk);
        SerializedProperty nodes = data.FindProperty("_nodes");
        string[] ids = new string[nodes.arraySize + 1];
        string[] names = new string[ids.Length];
        ids[0] = "";
        names[0] = "(지정 안 함)";
        for (int i = 0; i < nodes.arraySize; i++)
        {
            SerializedProperty node = nodes.GetArrayElementAtIndex(i);
            ids[i + 1] = node.FindPropertyRelative("id").stringValue;
            SerializedProperty lines = node.FindPropertyRelative("lines");
            string text = lines.arraySize > 0 ? lines.GetArrayElementAtIndex(0).stringValue :
                node.FindPropertyRelative("ask").stringValue;
            string id = ids[i + 1];
            CTalkQuestLink stages = talk.QuestLink;
            string label = id;
            if (stages.quest != null)
            {
                if (id == stages.offerNode) label = "수락 전";
                else if (id == stages.progressNode) label = "진행 중";
                else if (id == stages.reportNode) label = "보고 가능";
                else if (id == stages.doneNode) label = "완료 후";
            }
            names[i + 1] = label + " · " + text;
        }
        SerializedProperty link = data.FindProperty("_questLink");
        SerializedProperty start = data.FindProperty("_startNode");
        start.isExpanded = EditorGUILayout.Foldout(start.isExpanded, "시작 대사와 단계 연결", true);
        if (start.isExpanded)
        {
            Pick(data.FindProperty("_startNode"), "기본 시작 대사", ids, names);
            Pick(data.FindProperty("_summonedStartNode"), "보스 소환 시 시작", ids, names);
            if (link.FindPropertyRelative("quest").objectReferenceValue != null)
            {
                Pick(link.FindPropertyRelative("offerNode"), "수락 전", ids, names);
                Pick(link.FindPropertyRelative("progressNode"), "진행 중", ids, names);
                Pick(link.FindPropertyRelative("reportNode"), "보고 가능", ids, names);
                Pick(link.FindPropertyRelative("doneNode"), "완료 후", ids, names);
            }
        }
        for (int i = 0; i < nodes.arraySize; i++)
        {
            SerializedProperty node = nodes.GetArrayElementAtIndex(i);
            node.isExpanded = EditorGUILayout.Foldout(node.isExpanded, names[i + 1], true);
            if (!node.isExpanded) continue;
            EditorGUILayout.PropertyField(node.FindPropertyRelative("lines"), new GUIContent("대사 문장"), true);
            EditorGUILayout.PropertyField(node.FindPropertyRelative("ask"), new GUIContent("질문 (문장이 비었을 때)"));
            SerializedProperty choices = node.FindPropertyRelative("choices");
            for (int j = 0; j < choices.arraySize; j++)
            {
                SerializedProperty choice = choices.GetArrayElementAtIndex(j);
                EditorGUILayout.PropertyField(choice.FindPropertyRelative("text"), new GUIContent("선택지 " + (j + 1)));
                Pick(choice.FindPropertyRelative("to"), "선택하면 나올 대사", ids, names);
                EditorGUILayout.PropertyField(choice.FindPropertyRelative("questAction"), new GUIContent("의뢰 동작"));
                if (GUILayout.Button("이 선택지 삭제"))
                {
                    choices.DeleteArrayElementAtIndex(j);
                    break;
                }
            }
            if (GUILayout.Button("선택지 추가"))
            {
                choices.arraySize++;
                SerializedProperty choice = choices.GetArrayElementAtIndex(choices.arraySize - 1);
                choice.FindPropertyRelative("text").stringValue = "새 선택지";
                choice.FindPropertyRelative("to").stringValue = "";
                choice.FindPropertyRelative("questAction").enumValueIndex = 0;
            }
            if (choices.arraySize == 0)
            {
                Pick(node.FindPropertyRelative("next"), "다음 대사", ids, names);
                SerializedProperty end = node.FindPropertyRelative("end");
                EditorGUILayout.PropertyField(end, new GUIContent("마지막 동작 (Shop: 상점, Travel: 이동)"));
                if (end.enumValueIndex == (int)ETalkEnd.Travel)
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("dest"), new GUIContent("이동할 맵 키"));
            }
        }
        if (GUILayout.Button("대사 한 묶음 추가"))
        {
            data.ApplyModifiedProperties();
            Undo.RecordObject(talk, "대사 추가");
            var updated = new System.Collections.Generic.List<CTalkNode>(talk.Nodes);
            updated.Add(new CTalkNode { id = "line_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                lines = new[] { "새 대사" }, end = ETalkEnd.Close });
            talk.SetData(talk.StartNode, data.FindProperty("_summonedStartNode").stringValue, updated);
            EditorUtility.SetDirty(talk);
            return;
        }
        data.ApplyModifiedProperties();
    }

    private static void Pick(SerializedProperty field, string label, string[] ids, string[] names)
    {
        int index = Array.IndexOf(ids, field.stringValue);
        if (index < 0) EditorGUILayout.HelpBox("찾을 수 없는 연결: " + field.stringValue, MessageType.Warning);
        EditorGUI.BeginChangeCheck();
        int selected = EditorGUILayout.Popup(label, Mathf.Max(index, 0), names);
        if (EditorGUI.EndChangeCheck()) field.stringValue = ids[selected];
    }
}
