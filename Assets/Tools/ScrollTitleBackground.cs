using System.Collections.Generic;
using UnityEngine;

public class ScrollTitleBackground : MonoBehaviour
{
    [SerializeField] private List<SpriteRenderer> _backgroundImages;
    [SerializeField] private Transform _startPosition;
    [SerializeField] private Transform _endPosition;
    [SerializeField] private float _scrollSpeed = 0.5f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        foreach (SpriteRenderer image in _backgroundImages)
        {
            image.transform.Translate(Vector3.right * _scrollSpeed * Time.deltaTime);

            if (image.transform.position.x > _endPosition.position.x)
            {
                image.transform.position = new Vector3(_startPosition.position.x, image.transform.position.y, image.transform.position.z);
            }

        }
    }
}
