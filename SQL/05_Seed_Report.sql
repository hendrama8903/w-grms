-- =====================================================================
-- W-GRMS : menu Report (CKB)
--   Goods Receiving History (CKB), Outstanding Order (CKB), Delivery Delay Summary (CKB)
-- Report hanya baca data, jadi tidak ada function (tombol) khusus.
--
-- Jalankan di database GRMS (aman dijalankan ulang)
-- =====================================================================

MERGE TB_M_MENU AS T
USING (VALUES
	('M00004',    NULL,     'Report',                         'fa-bar-chart',      '#',                       4),
	('M00004-01', 'M00004', 'Goods Receiving History (CKB)',  'fa-history',        '/GrHistory/Index',        1),
	('M00004-02', 'M00004', 'Outstanding Order (CKB)',        'fa-hourglass-half', '/OutstandingOrder/Index', 2),
	('M00004-03', 'M00004', 'Delivery Delay Summary (CKB)',   'fa-clock-o',        '/SummaryDelay/Index',     3)
) AS S (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ)
ON T.MENU_ID = S.MENU_ID
WHEN NOT MATCHED THEN
	INSERT (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.MENU_ID, S.PARENT_ID, S.MENU_NAME, S.MENU_ICON, S.MENU_URL, S.MENU_SEQ, 0, 'system', GETDATE());
GO

-- ADMIN & USER: semua report (di desktop semua user punya akses report)
INSERT INTO TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, M.MENU_ID, 'system', GETDATE()
FROM (VALUES ('ADMIN'), ('USER')) R (ROLE_ID)
CROSS JOIN TB_M_MENU M
WHERE M.MENU_ID IN ('M00004', 'M00004-01', 'M00004-02', 'M00004-03')
  AND NOT EXISTS (SELECT 1 FROM TB_M_AUTH_MENU A WHERE A.ROLE_ID = R.ROLE_ID AND A.MENU_ID = M.MENU_ID);
GO

-- Nama menu lama "Summary Delay Delivery (CKB)" diganti ke ejaan yang benar
UPDATE TB_M_MENU SET MENU_NAME = 'Delivery Delay Summary (CKB)', CHANGED_BY = 'system', CHANGED_DT = GETDATE()
WHERE MENU_ID = 'M00004-03' AND MENU_NAME = 'Summary Delay Delivery (CKB)';
GO
