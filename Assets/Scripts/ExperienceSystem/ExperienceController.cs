using DevilSystem;
using Sirenix.OdinInspector;
using Sisus.Init;
using UnityEngine;

namespace ExperienceSystem
{
    [Service(typeof(ExperienceController), FindFromScene = true)]
    public class ExperienceController : MonoBehaviour<DevilController>
    {
        [SerializeField] private ExperienceData experienceData;
        
        private int experiencePoints;
        private int currentLevel = 0;
        private DevilController devilController;
        
        protected override void Init(DevilController devilController)
        {
            this.devilController = devilController;
            this.devilController.DevilController_StopLaugh += LevelUp;
        }

        [Button]
        public void AddExperience(int experienceGained)
        {
            experiencePoints += experienceGained;
            ChangeDevilState();
        }

        private void ChangeDevilState()
        {
            var expFillPercentage = Mathf.InverseLerp(0,experienceData.experienceNeededForLevelUp[currentLevel], experiencePoints);
            devilController.UpdateDevilStateDependingOnExperienceFill(expFillPercentage);
        }

        private void LevelUp()
        {
            Debug.Log("Leveled");
            experiencePoints = 0;
            currentLevel++;
            ChangeDevilState();
        }
    }
}
