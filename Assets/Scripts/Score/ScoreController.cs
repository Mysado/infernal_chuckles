using System;
using DG.Tweening;
using Sisus.Init;
using TMPro;
using UnityEngine;

namespace Score
{
    [Service(typeof(ScoreController), FindFromScene = true)]
    public class ScoreController : MonoBehaviour<ComboController>
    {
        [SerializeField] private TextMeshProUGUI scorePointsText;
        [SerializeField] private int scorePointsTextSizeNormal;
        [SerializeField] private int scorePointsTextSizeBig;
        [SerializeField] private float scorePointsTextShakeInterval;
        [SerializeField] private float scorePointsTextShakeStrength;
        
        private int score;
        private int scoreModifier;
        private ComboController comboController;
        private Sequence scoreTextSequence;

        protected override void Init(ComboController comboController)
        {
            this.comboController = comboController;
        }

        private void Start()
        {
            comboController = FindAnyObjectByType<ComboController>();

        }

        public int Score
        {
            get => score;
            set => score = value;
        }
        
        public int ScoreModifier
        {
            get => scoreModifier;
            set => scoreModifier = value;
        }
        
        public void AddScorePoints(int scorePointsToAdd)
        {
            scorePointsToAdd = 4;
            var comboPoints = scorePointsToAdd * comboController.ComboCounter / 100;
            Score += (scorePointsToAdd + comboPoints * scoreModifier);
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
            scoreTextSequence.OnComplete(() => scorePointsText.rectTransform.DOAnchorPos(new Vector2(-15,-65), 0.1f));
            scoreTextSequence.OnComplete(() => scorePointsText.DOScale(1, 0.1f));
        }
    }
}
