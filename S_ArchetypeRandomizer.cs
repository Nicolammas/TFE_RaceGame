using UnityEngine;

public class S_ArchetypeRandomizer : MonoBehaviour
{
    public enum Archetype
    {
        Vegetarien,
        Carnivore,
        Maniac,
        Gourmand,
        Alcoolique,
    }

    [Header("Archétype assigné")]
    [SerializeField] private Archetype assignedArchetype;

    public Archetype AssignedArchetype { get { return assignedArchetype; } }

    public Archetype AssignRandomArchetype()
    {
        int archetypeCount = System.Enum.GetValues(typeof(Archetype)).Length;
        int randomIndex = Random.Range(0, archetypeCount);

        return assignedArchetype = (Archetype)randomIndex;
    }
}
