using UnityEngine;

[CreateAssetMenu(fileName = "PlanetData", menuName = "ScriptableObjects/PlanetData", order = 1)]
public class PlanetData : ScriptableObject
{
      public string planetName;
    [TextArea(3, 10)] public string description;
    public Sprite planetImage; // optional if you want to show an image later
}
