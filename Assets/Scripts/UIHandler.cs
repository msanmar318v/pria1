using UnityEngine;

public class UIHandler : MonoBehaviour
{
    [SerializeField] private GameObject[] panels;

    void Start()
    {
        if (panels == null || panels.Length == 0) return;
        //ocultarPaneles();
    }

    public void ocultarPaneles()
    {
        foreach (var panel in panels)
        {
            panel.SetActive(false);
        }
    }

    
    void Update()
    {
        
    }
}
