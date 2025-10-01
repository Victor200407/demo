using UnityEngine;
using UnityEngine.UI;

public class OxygenSystem : MonoBehaviour
{
    [Header("Ustawienia tlenu")]
    public float maxOxygen  = 5f;  // sekundy "zasobu tlenu"
    public float drainRate  = 1f;   // ile sek./s ubywa pod ziemią
    public float regenRate  = 2f;   // ile sek./s przybywa na powierzchni

    [Header("UI (opcjonalne)")]
    public Slider   oxygenSlider;
    public Gradient colorGradient;
    public Image    fillImage;

    MoleRotateAroundController mole;
    float oxygen;
    bool  forcedExitTriggered;

    void Start()
    {
        mole = GetComponent<MoleRotateAroundController>();
        oxygen = maxOxygen;

        if (oxygenSlider != null)
        {
            oxygenSlider.maxValue = maxOxygen;
            oxygenSlider.value    = oxygen;
            oxygenSlider.value = maxOxygen;
        }
    }

    void Update()
    {
        if (mole == null) return;

        bool underground = mole.Depth > 0.05f;

        if (mole.IsForcedExiting)
        {
            // w trakcie wynurzania nie schodź poniżej 0
            if (underground) oxygen = Mathf.Max(0f, oxygen);
        }
        else
        {
            if (underground) oxygen -= drainRate * Time.deltaTime;
            else             oxygen += regenRate * Time.deltaTime;
        }

        oxygen = Mathf.Clamp(oxygen, 0f, maxOxygen);

        // UI
        if (oxygenSlider != null)
        {
            oxygenSlider.value = oxygen;
            if (fillImage != null && colorGradient != null)
                fillImage.color = colorGradient.Evaluate(oxygen / maxOxygen);
        }

        // Brak tlenu -> uruchom automatyczne wynurzenie (raz)
        if (oxygen <= 0f && !forcedExitTriggered)
        {
            forcedExitTriggered = true;
            mole.BeginForcedExit(); // wyłączy kopanie (spacja), odpali animację stop i wynurzy
        }

        // Gdy wynurzenie zakończone i stoimy na powierzchni -> pozwól na ponowne wyzwolenie oraz naturalną regenerację
        if (!mole.IsForcedExiting && !underground && forcedExitTriggered)
        {
            forcedExitTriggered = false;

            // (opcjonalnie) awaryjne 20% tlenu po wyjściu:
            // oxygen = Mathf.Max(oxygen, maxOxygen * 0.2f);
        }
    }

    // (opcjonalnie) public getter
    public float GetOxygen01() => oxygen / maxOxygen;
}
