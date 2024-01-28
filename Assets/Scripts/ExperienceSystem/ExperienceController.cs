using System;
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
        public DevilController devilController;
        private bool levelingUp;
        
        protected override void Init(DevilController devilController)
        {
            this.devilController = devilController;
            
        }

        private void Start()
        {
            devilController = FindAnyObjectByType<DevilController>();

            devilController.DevilController_ResetDevil += LevelUp;
        }

        [Button]
        public void AddExperience(int experienceGained)
        {
            if (levelingUp)
                return;
            
            experiencePoints += experienceGained;
            
            if (experiencePoints == experienceData.experienceNeededForLevelUp[currentLevel] && !levelingUp)
                levelingUp = true;
            
            ChangeDevilState();
        }

        private void ChangeDevilState()
        {
            var expFillPercentage = Mathf.InverseLerp(0,experienceData.experienceNeededForLevelUp[currentLevel], experiencePoints);
            devilController.UpdateDevilStateDependingOnExperienceFill(expFillPercentage);
        }

        private void LevelUp()
        {
            experiencePoints = 0;
            currentLevel++;
            levelingUp = false;
            ChangeDevilState();
        }
    }
}
