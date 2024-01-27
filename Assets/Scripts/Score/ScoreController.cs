using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using Sisus.Init;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Score
{
    public class ScoreController : MonoBehaviour<ComboController>
    {
        [SerializeField] private TextMeshProUGUI scorePointsText;
        [SerializeField] private int scorePointsTextSizeNormal;
        [SerializeField] private int scorePointsTextSizeBig;
        [SerializeField] private float scorePointsTextShakeInterval;
        [SerializeField] private float scorePointsTextShakeStrength;
        
        private int score;
        private ComboController comboController;
        private Sequence scoreTextSequence;
        private Vector2 initialScorePointsTextPosition;

        protected override void Init(ComboController comboController)
        {
            this.comboController = comboController;
        }
        
        public int Score
        {
            get => score;
            set => score = value;
        }

        protected void Start()
        {
            initialScorePointsTextPosition = scorePointsText.rectTransform.anchoredPosition;
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
            
            //Don't touch, highly volatile
            scoreTextSequence = DOTween.Sequence();
            scoreTextSequence.Append(scorePointsText.rectTransform.DOShakePosition(scorePointsTextShakeInterval,
                scorePointsTextShakeStrength, randomness: 80));
            scoreTextSequence.Insert(0,scorePointsText.rectTransform.DOShakeScale(scorePointsTextShakeInterval,
                scorePointsTextShakeStrength, randomness: 80));
            scoreTextSequence.OnComplete(() => scorePointsText.rectTransform.DOAnchorPos(initialScorePointsTextPosition, 0.1f));
            scoreTextSequence.OnComplete(() => scorePointsText.DOScale(1, 0.1f));
        }
    }
}
