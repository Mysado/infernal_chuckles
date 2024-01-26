using Sisus.Init;
using TMPro;
using UnityEngine;

namespace Score
{
   [Service]
   public class ComboController : MonoBehaviour
   {
      private readonly int maxComboCounterModifierLimit = 100;
      
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
         ChangeComboCounterTextDisplay();
      }

      private void RefreshComboCounterText()
      {
         comboCounterText.text = $"Combo x" + comboCounter;
      }
   
      private void ResetComboCounterTextDisplay()
      {
         comboCounterDisplayModifier = 0;
         comboCounterText.color = lowComboCounterDisplayColor;
         comboCounterText.fontSize = lowComboCounterDisplayTextSize;
      }

      private void SetComboCounterTextDisplayModifier()
      {
         if (comboCounter >= 100)
         {
            comboCounterDisplayModifier = 1;
         }
         else
         {
            comboCounterDisplayModifier = comboCounter / maxComboCounterModifierLimit;
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
