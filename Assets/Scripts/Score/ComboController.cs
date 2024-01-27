using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using Sisus.Init;
using TMPro;
using UnityEngine;

namespace Score
{
   [Service(typeof(ComboController),FindFromScene = true)]
   public class ComboController : MonoBehaviour
   {
      private readonly int maxComboCounterModifierLimit = 100;
      private readonly float comboCounterDisplayPunchDuration = 0.1f;
      
      [SerializeField] private TextMeshProUGUI comboCounterText;
      [SerializeField] private Color lowComboCounterDisplayColor;
      [SerializeField] private Color highComboCounterDisplayColor;
      [SerializeField] private int lowComboCounterDisplayTextSize;
      [SerializeField] private int highComboCounterDisplayTextSize;
   
      private int comboCounter = 1;
      private float comboCounterDisplayModifier;

      public int ComboCounter
      {
         get => comboCounter;
         set => comboCounter = value;
      }

      public void ResetComboCounter()
      {
         ComboCounter = 1;
         RefreshComboCounterText();
         ResetComboCounterTextDisplay();
      }

      public void IncreaseComboCounter()
      {
         ComboCounter++;
         RefreshComboCounterText();
         SetComboCounterTextDisplayModifier();
         ShakeComboCounterTextDisplay();
         ChangeComboCounterTextDisplay();
      }

      private void RefreshComboCounterText()
      {
         comboCounterText.text = $"Combo x{comboCounter}";
      }
   
      private void ResetComboCounterTextDisplay()
      {
         comboCounterDisplayModifier = 0;
         comboCounterText.color = lowComboCounterDisplayColor;
         comboCounterText.fontSize = lowComboCounterDisplayTextSize;
      }

      private void ShakeComboCounterTextDisplay()
      {
         var punchStrength = comboCounterDisplayModifier * 10;
         var comboCounterShakeSequence = DOTween.Sequence();
         comboCounterShakeSequence.Append(
            comboCounterText.rectTransform.DOShakePosition(comboCounterDisplayPunchDuration, punchStrength,
               randomness: 80).SetLoops(2));
         comboCounterShakeSequence.OnComplete(() => comboCounterText.rectTransform.DOAnchorPos(new Vector2(-15, -15), 0.5f));
      }

      private void SetComboCounterTextDisplayModifier()
      {
         if (comboCounter >= 100)
         {
            comboCounterDisplayModifier = 1;
         }
         else
         {
            comboCounterDisplayModifier = (float)comboCounter / maxComboCounterModifierLimit;
         }
      }

      private void ChangeComboCounterTextDisplay()
      {
         comboCounterText.color = Color.Lerp(lowComboCounterDisplayColor, highComboCounterDisplayColor,
            comboCounterDisplayModifier);
         comboCounterText.fontSize = Mathf.Lerp(lowComboCounterDisplayTextSize,highComboCounterDisplayTextSize,
            comboCounterDisplayModifier);
      
      }
   }
}
