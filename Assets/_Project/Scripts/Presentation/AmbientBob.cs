using UnityEngine;

namespace ChoNoi.Presentation
{
    public class AmbientBob : MonoBehaviour
    {
        [SerializeField] private Vector3 bobAxis = new Vector3(0f, 0.35f, 0f);
        [SerializeField] private Vector3 swayAxis = new Vector3(0f, 12f, 0f);
        [SerializeField] private float bobSpeed = 0.8f;
        [SerializeField] private float swaySpeed = 1.2f;
        [SerializeField] private float phaseOffset;

        private Vector3 startPosition;
        private Quaternion startRotation;

        public Vector3 StartPosition
        {
            get => startPosition;
            set => startPosition = value;
        }

        private void Awake()
        {
            startPosition = transform.localPosition;
            startRotation = transform.localRotation;
        }

        private void Update()
        {
            float time = Time.time * bobSpeed + phaseOffset;
            transform.localPosition = startPosition + bobAxis * Mathf.Sin(time);
            transform.localRotation = startRotation * Quaternion.Euler(swayAxis * Mathf.Sin(Time.time * swaySpeed + phaseOffset));
        }
    }
}
