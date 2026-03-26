WITH CTE AS(
SELECT 
	CASE WHEN TW.Wins >= TL.Wins THEN TW.Wins ELSE TL.Wins END AS W1, 
	CASE WHEN TW.Wins >= TL.Wins THEN TL.Wins ELSE TW.Wins END AS W2, 
	CASE WHEN TW.Wins >= TL.Wins THEN g.WPoints - g.LPoints ELSE g.LPoints - g.WPoints END AS Delta
FROM dbo.Game G
INNER JOIN dbo.TeamRecords TW
	ON G.WinnerId = TW.TeamID
	AND G.Year = TW.Year
INNER JOIN dbo.TeamRecords TL
	ON G.LoserId = TL.TeamID
	AND G.Year = TL.Year
)
SELECT 
	CTE.W1, 
	CTE.W2, 
	ROUND(AVG(CTE.Delta), 0) AvgDelta, 
	STDEVP(CTE.Delta) StdDevP,
	Count(*) SampleSize
FROM CTE
GROUP BY CTE.W1, CTE.W2
ORDER BY CTE.W1, CTE.W2
