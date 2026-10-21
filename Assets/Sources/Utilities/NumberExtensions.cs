using UnityEngine;

namespace Sources
{
    public static class NumbersExtensions
    {
        public static float ClosestOf(this float a, params float[] values)
        {
            float min = Mathf.Abs(values[0] - a);
            int minIndex = 0;

            for (int i = 1; i < values.Length; i++)
            {
                var value = Mathf.Abs(values[i] - a);

                if (value < min)
                {
                    min = value;
                    minIndex = i;
                }
            }

            return values[minIndex];
        }

        public static float ClosestOf(this float a, float number1, float number2)
        {
            var delta1 = Mathf.Abs(a - number1);
            var delta2 = Mathf.Abs(a - number2);

            return delta1 > delta2 ? number2 : number1;
        }

        public static float Snap(this float a, float to)
        {
            var min = a - a % to;
            return a.ClosestOf(min, min + to);
        }
    }
}
