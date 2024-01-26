using Sisus.Init;
using TMPro;
using UnityEngine;

namespace Score
{
    public class ScoreController : MonoBehaviour<ComboController>
    {
        [SerializeField] private TextMeshProUGUI scorePointsText;
        
        private int score;
        private ComboController comboController;

        protected override void Init(ComboController comboController)
        {
            this.comboController = comboController;
        }

        public int Score
        {
            get => score;
            set => score = value;
        }

        public void AddScorePoints(int scorePointsToAdd)
        {
            var comboPoints = scorePointsToAdd * comboController.ComboCounter / 100;
            Score += (scorePointsToAdd + comboPoints);
            RefreshScorePointsText();
        }

        public void DeductScorePoints(int scorePointsToDeduct)
        {
            Score -= scorePointsToDeduct;
            RefreshScorePointsText();
        }

        private void RefreshScorePointsText()
        {
            scorePointsText.text = Score.ToString();
            //do some cool animation here
        }
    }
}
