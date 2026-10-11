using System.Collections.Generic;
using UnityEngine;

namespace MuckClone.World
{
    /// <summary>
    /// Algoritmo de Poisson Disc Sampling 2D (Bridson).
    /// Gera pontos aleatórios uniformemente distribuídos com garantia de distância mínima 'radius' entre eles.
    /// Utilizado para posicionamento natural de árvores, rochas e baús sem sobreposição.
    /// </summary>
    public static class PoissonDiscSampler
    {
        public static List<Vector2> GeneratePoints(float radius, Vector2 sampleRegionSize, int numSamplesBeforeRejection = 30, int seed = 0)
        {
            var prng = new System.Random(seed);
            float cellSize = radius / Mathf.Sqrt(2);

            int gridWidth = Mathf.CeilToInt(sampleRegionSize.x / cellSize);
            int gridHeight = Mathf.CeilToInt(sampleRegionSize.y / cellSize);
            int[,] grid = new int[gridWidth, gridHeight];

            List<Vector2> points = new List<Vector2>();
            List<Vector2> spawnPoints = new List<Vector2>();

            // Ponto inicial no centro da região
            Vector2 initialPoint = sampleRegionSize * 0.5f;
            spawnPoints.Add(initialPoint);

            while (spawnPoints.Count > 0)
            {
                int spawnIndex = prng.Next(0, spawnPoints.Count);
                Vector2 spawnCentre = spawnPoints[spawnIndex];
                bool candidateAccepted = false;

                for (int i = 0; i < numSamplesBeforeRejection; i++)
                {
                    float angle = (float)prng.NextDouble() * Mathf.PI * 2f;
                    Vector2 dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                    float candidateDist = radius + (float)prng.NextDouble() * radius;
                    Vector2 candidate = spawnCentre + dir * candidateDist;

                    if (IsValid(candidate, sampleRegionSize, cellSize, radius, points, grid))
                    {
                        points.Add(candidate);
                        spawnPoints.Add(candidate);
                        int cellX = (int)(candidate.x / cellSize);
                        int cellY = (int)(candidate.y / cellSize);
                        grid[cellX, cellY] = points.Count;
                        candidateAccepted = true;
                        break;
                    }
                }

                if (!candidateAccepted)
                {
                    spawnPoints.RemoveAt(spawnIndex);
                }
            }

            return points;
        }

        private static bool IsValid(Vector2 candidate, Vector2 sampleRegionSize, float cellSize, float radius, List<Vector2> points, int[,] grid)
        {
            if (candidate.x < 0 || candidate.x >= sampleRegionSize.x || candidate.y < 0 || candidate.y >= sampleRegionSize.y)
            {
                return false;
            }

            int cellX = (int)(candidate.x / cellSize);
            int cellY = (int)(candidate.y / cellSize);

            int searchStartX = Mathf.Max(0, cellX - 2);
            int searchEndX = Mathf.Min(cellX + 2, grid.GetLength(0) - 1);
            int searchStartY = Mathf.Max(0, cellY - 2);
            int searchEndY = Mathf.Min(cellY + 2, grid.GetLength(1) - 1);

            float sqrRadius = radius * radius;

            for (int x = searchStartX; x <= searchEndX; x++)
            {
                for (int y = searchStartY; y <= searchEndY; y++)
                {
                    int pointIndex = grid[x, y] - 1;
                    if (pointIndex != -1)
                    {
                        float sqrDst = (candidate - points[pointIndex]).sqrMagnitude;
                        if (sqrDst < sqrRadius)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
