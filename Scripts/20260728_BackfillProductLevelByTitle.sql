-- 2026-07-28
-- 依商品名稱回填 Products.Level（A~E）
-- 規則：若同時命中多個 Pattern，採 Priority 最小者。

SET NOCOUNT ON;

-- 1) 先設預設 C
UPDATE p
SET p.Level = N'C'
FROM Products p;

DECLARE @Rules TABLE
(
    Priority INT NOT NULL,
    Pattern NVARCHAR(200) NOT NULL,
    [Level] NCHAR(1) NOT NULL
);

-- A
INSERT INTO @Rules (Priority, Pattern, [Level]) VALUES
(10, N'%Picotin Lock 18%', N'A'),
(11, N'%Evelyne 16 Amazone%', N'A'),
(12, N'%Roulis%', N'A'),
(13, N'%Lindy 26%', N'A'),
(14, N'%Halzan迷你%', N'A'),
(15, N'%In-the-Loop 18%', N'A');

-- B
INSERT INTO @Rules (Priority, Pattern, [Level]) VALUES
(20, N'%Picotin Lock 22%', N'B'),
(21, N'%Evelyne 23 Poche III%', N'B'),
(22, N'%24/24 - 21%', N'B'),
(23, N'%Garden Party 30%', N'B'),
(24, N'%Herbag Zip 20%', N'B'),
(25, N'%Herbag Zip 31%', N'B'),
(26, N'%Halzan 25%', N'B'),
(27, N'%Kelly depeches 25%', N'B'),
(28, N'%Kelly郵差包%', N'B'),
(29, N'%Jypsiere迷你%', N'B'),
(30, N'%Geta Slim%', N'B'),
(31, N'%Neo Garden 23%', N'B'),
(32, N'%Evelyne III 29%', N'B');

-- C
INSERT INTO @Rules (Priority, Pattern, [Level]) VALUES
(40, N'%Poche Cliquetis%', N'C'),
(41, N'%Videpoches%', N'C'),
(42, N'%So Medor%', N'C'),
(43, N'%Neo Medor%', N'C'),
(44, N'%Steve light junior%', N'C'),
(45, N'%Sac a depeches 21%', N'C'),
(46, N'%Sac a depeches light 1-36%', N'C'),
(47, N'%Bolide%', N'C'),
(48, N'%Le Petit Sac%', N'C'),
(49, N'%Steeple 25%', N'C'),
(50, N'%Steeple 28%', N'C'),
(51, N'%Maximors II%', N'C'),
(52, N'%Maximors%', N'C'),
(53, N'%Hac a Dos PM%', N'C'),
(54, N'%Hac a Dos GM%', N'C'),
(55, N'%Jypsiere mini Toile & Cuir%', N'C');

-- D
INSERT INTO @Rules (Priority, Pattern, [Level]) VALUES
(60, N'%Cab''H%', N'D'),
(61, N'%Medor手提包%', N'D'),
(62, N'%En Piste%', N'D'),
(63, N'%Tout en Carre%', N'D'),
(64, N'%Balusoie%', N'D'),
(65, N'%Fonsbelle Chaine%', N'D'),
(66, N'%Herbag Messenger 39%', N'D'),
(67, N'%Harnacheur%', N'D'),
(68, N'%Onbody Etriviere%', N'D'),
(69, N'%Collier d''Attelage%', N'D'),
(70, N'%Della Cavalleria Elan%', N'D'),
(71, N'%Messenger 57%', N'D');

-- E
INSERT INTO @Rules (Priority, Pattern, [Level]) VALUES
(80, N'%Sanglons%', N'E'),
(81, N'%Lassoie%', N'E');

;WITH Pick AS
(
    SELECT
        p.Id,
        r.[Level],
        ROW_NUMBER() OVER (PARTITION BY p.Id ORDER BY r.Priority) AS rn
    FROM Products p
    JOIN @Rules r
        ON p.Title LIKE r.Pattern
)
UPDATE p
SET p.Level = x.[Level]
FROM Products p
JOIN Pick x
    ON p.Id = x.Id
   AND x.rn = 1;

-- 驗證
SELECT [Level], COUNT(*) AS Cnt
FROM Products
GROUP BY [Level]
ORDER BY [Level];
