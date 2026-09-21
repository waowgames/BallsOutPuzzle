using System.Collections;
using UnityEngine;

public class RotateAround : MonoBehaviour
{
    [SerializeField] private float rotateSpeed;
    [SerializeField] private float rotateAxis; // 0 = X, 1 = Y, 2 = Z
    private Transform _transform;

    private void Start()
    {
        _transform = GetComponent<Transform>();
    }

    private void Update()
    {
        RotateAroundUpdate();
    }

    private void RotateAroundUpdate()
    {
        _transform.Rotate(
            rotateAxis == 0 ? 30 * Time.deltaTime * rotateSpeed : 0,
            rotateAxis == 1 ? 30 * Time.deltaTime * rotateSpeed : 0,
            rotateAxis == 2 ? 30 * Time.deltaTime * rotateSpeed : 0
        );
    }

    public void SetSpeed(float speed)
    {
        rotateSpeed = speed;
    }
}
