using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [SerializeField] private Animator lucjusz;
    [SerializeField] private AudioSource laughSFX;
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void LoadGame()
    {
        DOTween.Sequence().AppendCallback(() =>
        {
        lucjusz.Play("Armature|Laugh03", 0, 0);
        laughSFX.Play();
        }).AppendInterval(5f)
            .AppendCallback(() => SceneManager.LoadScene("Prototype1"));
    }
}
