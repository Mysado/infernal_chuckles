using System.Collections.Generic;
using UnityEngine;

namespace ExperienceSystem
{
    [CreateAssetMenu(fileName = "ExperienceData", menuName = "Experience System")]
    public class ExperienceData : ScriptableObject
    {
        public List<int> experienceNeededForLevelUp;
    }
}
