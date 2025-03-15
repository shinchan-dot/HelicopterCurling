#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class TreeScript : MonoBehaviour
{
    public GameObject Prefab;
    public Transform Parent;

    [ContextMenu("PUT_TREES")]
    public void PutPlanes()
    {
        int count = 1;
        while (true)
        {
            //float x = Random.Range(-2100, 2100);
            //float z = Random.Range(-1920, 2280);
            float x = Random.Range(-600, 600);
            float z = Random.Range(-920, 1280);
            if (-200 <= x && x <= 200  && - 520 <= z && z <= 880)
            {
                continue;
            }

            GameObject obj = PrefabUtility.InstantiatePrefab(Prefab) as GameObject;

            obj.name = "Tree (" + count + ")";

            Transform tran = obj.GetComponent<Transform>();
            tran.localScale = new Vector3(5, 5, 5);
            tran.localPosition = new Vector3(x, 0, z);
            tran.rotation = Quaternion.Euler(0, Random.Range(0, 359), 0);
            tran.SetParent(Parent);

            count++;
            if (count > 200)
            {
                break;
            }
        }
    }
}
#endif
