using System;
using UnityEngine;

public enum ETutorialKeyNow
{
    [InspectorName("없음")] None = 0,
    [InspectorName("Q 연계 열림")] LinkOpen = 1,
    [InspectorName("W 연계 가능")] CrownOpen = 2,
    [InspectorName("레비아탄 최대 중첩")] LeviathanFull = 3,
    [InspectorName("허수아비 낙인 최대 중첩")] MarksFull = 4,
}

public enum ETutorialKeyDone
{
    [InspectorName("없음")] None = 0,
    [InspectorName("카메라 돌림")] CameraRotated = 1,
    [InspectorName("카메라 줌")] CameraZoomed = 2,
    [InspectorName("레비아탄 깨움")] LeviathanAwake = 3,
    [InspectorName("권능 받음")] PowerReceived = 4,
}

[Serializable]
public class CTutorialKey
{
    public string key;
    public ETutorialKeyIcon icon;
    public string plusKey;
    public ETutorialKeyIcon plusIcon;
    public string altKey;
    public string caption;
    public ETutorialKeyNow nowWhen;
    public ETutorialKeyDone doneWhen;
}

/// <summary>
/// 퀘스트 목표 ID에 연결된 대사, 입력 안내와 훈련 설정입니다.
/// </summary>
[Serializable]
public class CTutorialStep
{
    public string objectiveId;
    [Tooltip("목표 시작 대사 ID. 비우면 대화를 생략합니다.")]
    public string introNode;
    public string equipObjectiveId;
    public string equippedIntroNode;
    [Tooltip("목표 안내 자막. {0}에 현재 진행 수량이 표시됩니다.")]
    public string remind;
    public CTutorialKey[] keys;
    [Tooltip("PlayerHud에서 강조할 슬롯의 상대 경로")]
    public string highlightPath;
    [Tooltip("허수아비와의 거리 및 빗나간 공격 안내")]
    public bool isTraining;
    public CSkillSO[] assistSkills;
    [Tooltip("이 단계에서 질투 권능을 임시로 부여합니다.")]
    public bool grantsPower;
}

[Serializable]
public class CTutorialPrompts
{
    public string openBag;
    public string equipItem;
    public string closeBag;
    public string talkToPradona;
    public string approachPradona;
    public string talkToFluffy;
    public string approachFluffy;
    public string approachScarecrow;
    public string missedLink;
    public string needsMarks;
    public string powerReady;
    public string crownWindowClosed;
    public string skillReady;
    public string manaRestored;
    public string finisherTooEarly;
    public string outOfRange;
    public string missedAttack;
    public string bagFull;
}
