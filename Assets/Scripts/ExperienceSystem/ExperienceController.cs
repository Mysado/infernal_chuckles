using DevilSystem;
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
        }

        public void AddExperience(int experienceGained)
        {
            experiencePoints += experienceGained;
            CheckIfLeveled();
            ChangeDevilState();
        }

        private void CheckIfLeveled()
        {
            if (experiencePoints >= experienceData.experienceNeededForLevelUp[currentLevel])
            {
                experiencePoints = 0;
                currentLevel++;
            }
        }

        private void ChangeDevilState()
        {
            var expFillPercentage = Mathf.Clamp(experiencePoints / experienceData.experienceNeededForLevelUp[currentLevel],0,1);
            devilController.UpdateDevilStateDependingOnExperienceFill(expFillPercentage);
        }
    }
}
