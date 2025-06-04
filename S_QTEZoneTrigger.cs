using DG.Tweening;
using Player.Movement;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class S_QTEZoneTrigger : MonoBehaviour
{
    [SerializeField] Transform kartPosition;
    [SerializeField] Transform playerPosition;
    [SerializeField] private Transform rightPortique;
    [SerializeField] private Transform LeftPortique;

    private List<string> itemList = new List<string>();
    private S_KartMove kart;
    private GameObject kartGO;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Vehicle"))
        {
            kartGO = other.gameObject;
            S_ItemListingForQTE listQTE = other.GetComponentInChildren<S_ItemListingForQTE>();
            if (listQTE == null) return;

            if (itemList == null)
                itemList = new List<string>();
            else
                itemList.Clear();

            foreach (ItemInfo itemName in listQTE.ItemList)
            {
                itemList.Add(itemName.itemName);
            }

            return;
        }

        if (!other.CompareTag("Player") || itemList == null || kartGO == null || itemList.Count == 0) return;

        kart = kartGO.GetComponent<S_KartMove>();
        kart.CanCollide = false;
        kart.StopKart();
        kartGO.transform.position = kartPosition.position;

        if (other.transform.parent == null || other.transform.parent.parent == null) return;
        Transform player = other.transform.parent.parent;

        other.GetComponent<SCR_Player_MovementController>().ExitVehicleAtCasher();
        player.position = playerPosition.position;

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        S_QTEItemSequenceManager qteManager = player.GetComponent<S_QTEItemSequenceManager>();
        S_ArchetypeRandomizer archetype = player.GetComponent<S_ArchetypeRandomizer>();

        if (qteManager == null || playerInput == null || archetype == null) return;

        qteManager.Initialize(playerInput, itemList, playerInput.playerIndex, archetype.AssignedArchetype);
        qteManager.StartItemQTE();

        other.GetComponent<SCR_Player_MovementController>().SetMovementEnabled(false);

        CloseDoor();
    }

    public void CloseDoor()
    {
        rightPortique.DOLocalRotate(new Vector3(0, 0, 0), 1.0f).SetEase(Ease.OutCubic);
        LeftPortique.DOLocalRotate(new Vector3(0, 0, 0), 1.0f).SetEase(Ease.OutCubic);
    }
}
