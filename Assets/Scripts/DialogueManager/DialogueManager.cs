using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CDialogueManager : MonoBehaviour
{
    public TMP_Text dialogueText;
    public string[] dialogueLines;
    public float typingSpeed = 0.05f;
    private int currentLine = 0;
    private bool isTyping = false;
    private Coroutine typingCoroutine;

    private void Start()
    {
        if (dialogueLines.Length > 0)
        {
            ShowLine();
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)||
            Input.GetKeyDown(KeyCode.Space)||
            Input.GetKeyDown(KeyCode.Return)||
            Input.GetKey(KeyCode.LeftControl))
        {
            if (isTyping)
            {
                CompleteLine();
            }

            else
            {
                NextLine();
            }
        }
    }

    private void ShowLine()
    {
        typingCoroutine = StartCoroutine(TypeLine(dialogueLines[currentLine]));
    }

    private IEnumerator TypeLine(string line)
    {
        dialogueText.text = "";
        isTyping = true;

        for (int i = 0; i < line.Length; i++)
        {
            dialogueText.text += line[i];

            yield return new WaitForSeconds(typingSpeed);
        }
        isTyping = false;
        typingCoroutine = null;
    }

    private void CompleteLine()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        dialogueText.text = dialogueLines[currentLine];
        isTyping = false;
    }

    private void NextLine()
    {
        currentLine++;

        if (currentLine < dialogueLines.Length)
        {
            ShowLine();
        }
        else
        {
            dialogueText.text = "";
        }
    }
}
