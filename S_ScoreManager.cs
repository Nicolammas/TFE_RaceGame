using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using static S_ArchetypeRandomizer;

public class S_ScoreManager : MonoBehaviour
{
    public static S_ScoreManager Instance { get; private set; }

    private readonly int[] finishPoints = { 15, 10, 5, 0 };
    public int[] FinishPoint => finishPoints;
    private const int FullListBonus = 5;
    private List<PlayerScore> finishedPlayers = new List<PlayerScore>();
    private List<string> masterItemList;
    public List<PlayerScore> FinishedPlayers => finishedPlayers;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        masterItemList = S_ShoppingListManager.SharedShoppingList;
    }

    /// <summary>
    /// Quand quelqu'un fini le QTE on envoie ici
    /// </summary>
    public void RegisterPlayerFinish(int playerId, List<string> itemName, Archetype playerArchetype)
    {
        if (finishedPlayers.Any(p => p.PlayerId == playerId))
            return;

        foreach (var player in masterItemList)
            Debug.Log(player);
        int correctItemCount = itemName.Count(name => masterItemList.Contains(CleanItemName(name)));
        bool fullSet = masterItemList.All(item => itemName.Any(scan => CleanItemName(scan) == item));
        int archetypeItemCount = itemName.Count(item =>
        {
            var itemArch = GetItemArchetypeFromName(item);
            return itemArch.HasValue && itemArch.Value == playerArchetype;
        });

        finishedPlayers.Add(new PlayerScore
        {
            PlayerId = playerId,
            CorrectScannedCount = correctItemCount,
            FullSet = fullSet,
            ArchetypeItems = archetypeItemCount,
            FinishTime = S_TimerMaster.Instance.CurrentTime
        });

        if (finishedPlayers.Count >= GetTotalPlayers())
        {
            S_GameState.Instance.TransitionToResult();
        }
    }

    private int GetTotalPlayers()
    {
        return S_GameState.Instance.Players.Count;
    }

    public void ComputeFinalScores()
    {
        for (int i = 0; i < finishedPlayers.Count; i++)
        {
            var ps = finishedPlayers[i];

            ps.ArrivalPosition = i + 1;

            int finishBonus = (i < finishPoints.Length) ? finishPoints[i] : 0;
            int itemsPoints = ps.CorrectScannedCount;
            int archetypePoints = ps.ArchetypeItems * 2;
            Debug.Log(archetypePoints);
            int fullBonus = ps.FullSet ? FullListBonus : 0;

            ps.TotalPoints = finishBonus + itemsPoints + fullBonus + archetypePoints;
        }

        var sortedByScore = finishedPlayers
            .OrderByDescending(p => p.TotalPoints)
            .ThenBy(p => p.FinishTime)
            .ToList();

        for (int i = 0; i < sortedByScore.Count; i++)
        {
            sortedByScore[i].FinalRanking = i + 1;
        }

        finishedPlayers = sortedByScore;
    }

    public void DisplayResults()
    {
        foreach (var ps in finishedPlayers)
        {
            Debug.Log(
                $"Player {ps.PlayerId} | Arrival #{ps.ArrivalPosition} | Final Rank #{ps.FinalRanking} | Total Points: {ps.TotalPoints} | Correct Items: {ps.CorrectScannedCount}");
        }

       
    }

    private Archetype? GetItemArchetypeFromName(string itemName)
    {
        if (itemName.EndsWith("(Clone)"))
            itemName = itemName.Replace("(Clone)", "");

        var match = Regex.Match(itemName, @"_([A-Z])$");
        if (!match.Success) return null;

        switch (match.Groups[1].Value)
        {
            case "V": return Archetype.Vegetarien;
            case "C": return Archetype.Carnivore;
            case "M": return Archetype.Maniac;
            case "G": return Archetype.Gourmand;
            case "A": return Archetype.Alcoolique;
            default: return null;
        }
    }

    private string CleanItemName(string fullName)
    {
        if (fullName.EndsWith("(Clone)"))
            fullName = fullName.Replace("(Clone)", "");

        var match = Regex.Match(fullName, @"^PFB_(.+?)_[A-Z]$");
        return match.Success ? match.Groups[1].Value : fullName;
    }

    /// <summary>
    /// Reset entre partie
    /// </summary>
    public void ResetScores()
    {
        finishedPlayers.Clear();
    }
}

public class PlayerScore
{
    public int PlayerId;
    public int CorrectScannedCount;
    public bool FullSet;
    public int ArrivalPosition;
    public int FinalRanking;
    public int TotalPoints;
    public int ArchetypeItems;
    public DateTime FinishTime;
}
