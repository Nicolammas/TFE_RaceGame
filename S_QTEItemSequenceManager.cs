using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using static S_ArchetypeRandomizer;

public class S_QTEItemSequenceManager : MonoBehaviour
{
    [Header("Items à scanner")]
    [Tooltip("Liste des items à traiter en caisse")]
    [SerializeField] private List<string> itemsToScan = new List<string>();

    [Header("Configuration QTE")]
    [SerializeField] private int minSequenceLength = 4;
    [SerializeField] private int maxSequenceLength = 6;
    [SerializeField] private float inputTimeLimit = 0.5f;

    [Header("UI & Feedback")]
    [SerializeField] private GameObject qteUI;
    [SerializeField] private Transform keyContainer;
    [SerializeField] private GameObject keyUIPrefab;
    [SerializeField] private Image timerFillImage;
    [SerializeField] private TMP_Text remainingItemsText;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip successClip;
    [SerializeField] private AudioClip failClip;

    private int playerId;
    private int currentItemIndex = 0;
    private Archetype playerArchetype;
    private List<string> currentSequence;
    private int currentInputIndex;
    private float inputTimer;
    private bool waitingForInput;
    private bool qteActive;
    private PlayerInput playerInput;
    private InputActionMap qteActionMap;
    private InputActionMap playerMovement;
    private Dictionary<string, InputAction> qteActions = new Dictionary<string, InputAction>();
    private Dictionary<string, string> inputDisplayNames = new Dictionary<string, string>
{
    { "QTE_UpArrow", "↑" },
    { "QTE_DownArrow", "↓" },
    { "QTE_LeftArrow", "←" },
    { "QTE_RightArrow", "→" }
};

    private void Awake()
    {
        qteUI.SetActive(false);
        UpdateRemainingItemsUI();
    }

    public void Initialize(PlayerInput input, List<string> masterList, int id, Archetype archetype)
    {
        playerInput = input;
        itemsToScan = new List<string>(masterList);
        playerId = id;
        playerArchetype = archetype;
        currentItemIndex = 0;
        UpdateRemainingItemsUI();

        qteActionMap = playerInput.actions.FindActionMap("QTE", true);
        playerMovement = playerInput.actions.FindActionMap("Player", true);
        playerMovement.Disable();
        foreach (var act in qteActionMap.actions)
        {
            qteActions[act.name] = act;
        }
    }

    public void StartItemQTE()
    {
        if (currentItemIndex >= itemsToScan.Count) return;

        GenerateSequenceForCurrentItem();
        qteActive = true;
        qteUI.SetActive(true);
        currentInputIndex = 0;
        ShowFullSequenceUI();
        EnableQTEInputs();
        StartNextInput();
    }

    private void EnableQTEInputs()
    {
        qteActionMap.Enable();
        foreach (var kv in qteActions)
        {
            kv.Value.performed += OnQTEInput;
        }
    }

    private void DisableQTEInputs()
    {
        foreach (var kv in qteActions)
        {
            kv.Value.performed -= OnQTEInput;
        }
        qteActionMap.Disable();
    }

    private void OnQTEInput(InputAction.CallbackContext ctx)
    {
        if (!playerInput.devices.Contains(ctx.control.device)) return;

        if (!waitingForInput) return;

        waitingForInput = false;
        string inputName = ctx.action.name;

        bool correct = inputName == currentSequence[currentInputIndex];
        audioSource.PlayOneShot(correct ? successClip : failClip);

        HighlightKey(currentInputIndex, correct);

        if (correct)
        {
            currentInputIndex++;
            if (currentInputIndex >= currentSequence.Count)
                OnSequenceSuccess();
            else
                StartNextInput();
        }
        else
        {
            OnSequenceFail();
        }
    }

    private void Update()
    {
        if (!qteActive || !waitingForInput) return;

        inputTimer -= Time.deltaTime;
        timerFillImage.fillAmount = inputTimer / inputTimeLimit;
        if (inputTimer <= 0f)
            OnSequenceFail();
    }

    private void OnSequenceSuccess()
    {
        currentItemIndex++;
        DisableQTEInputs();
        qteUI.SetActive(false);

        if (currentItemIndex >= itemsToScan.Count)
        {
            S_ScoreManager.Instance.RegisterPlayerFinish(playerId, itemsToScan, playerArchetype);
        }
        else
        {
            UpdateRemainingItemsUI();
            StartItemQTE();
        }
    }

    private void OnSequenceFail()
    {
        DisableQTEInputs();
        qteUI.SetActive(false);
        GenerateSequenceForCurrentItem();
        StartItemQTE();
    }

    private void StartNextInput()
    {
        inputTimer = inputTimeLimit;
        waitingForInput = true;
    }

    private void GenerateSequenceForCurrentItem()
    {
        int length = UnityEngine.Random.Range(minSequenceLength, maxSequenceLength + 1);
        currentSequence = new List<string>();
        var keys = qteActions.Keys.ToList();
        for (int i = 0; i < length; i++)
            currentSequence.Add(keys[UnityEngine.Random.Range(0, keys.Count)]);
    }

    private void ShowFullSequenceUI()
    {
        foreach (Transform c in keyContainer) Destroy(c.gameObject);
        foreach (var name in currentSequence)
        {
            var go = Instantiate(keyUIPrefab, keyContainer);
            var txt = go.GetComponentInChildren<TMP_Text>();
            txt.text = (inputDisplayNames.ContainsKey(name) ? inputDisplayNames[name] : name.Replace("QTE_", ""));
        }
    }

    private void HighlightKey(int idx, bool success)
    {
        var img = keyContainer.GetChild(idx).GetComponentInChildren<Image>();
        img.color = success ? Color.green : Color.red;
    }

    private void UpdateRemainingItemsUI()
    {
        remainingItemsText.text = $"Items scannés: {currentItemIndex}/{itemsToScan.Count}";
    }
}
