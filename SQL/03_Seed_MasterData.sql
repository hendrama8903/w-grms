-- =====================================================================
-- W-GRMS : menu & function Master Data (CKB)
--   Parts Master (CKB)    -> OLKPAM
--   Supplier Master (CKB) -> OLKSUP
-- Sumber "Update from CKB" (linked server per plant) ada di appsettings.json "CkbSources".
--
-- Jalankan di database GRMS (aman dijalankan ulang)
-- =====================================================================

MERGE TB_M_MENU AS T
USING (VALUES
	('M00002',    NULL,     'Master Data',           'fa-database', '#',                       2),
	('M00002-01', 'M00002', 'Parts Master (CKB)',    'fa-cubes',    '/PartsMaster/Index',      1),
	('M00002-02', 'M00002', 'Supplier Master (CKB)', 'fa-truck',    '/SupplierMaster/Index',   2)
) AS S (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ)
ON T.MENU_ID = S.MENU_ID
WHEN NOT MATCHED THEN
	INSERT (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.MENU_ID, S.PARENT_ID, S.MENU_NAME, S.MENU_ICON, S.MENU_URL, S.MENU_SEQ, 0, 'system', GETDATE());
GO

MERGE TB_M_FUNCTION AS T
USING (VALUES
	('PARTS-SYNC',      'M00002-01', 'Update',  'Update parts from CKB database'),
	('PARTS-DELETE',    'M00002-01', 'Delete',  'Delete part'),
	('SUPPLIER-SYNC',   'M00002-02', 'Update',  'Update suppliers from CKB databases'),
	('SUPPLIER-DELETE', 'M00002-02', 'Delete',  'Delete supplier')
) AS S (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC)
ON T.FUNCTION_ID = S.FUNCTION_ID
WHEN NOT MATCHED THEN
	INSERT (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.FUNCTION_ID, S.MENU_ID, S.FUNCTION_NAME, S.FUNCTION_DESC, 0, 'system', GETDATE());
GO

-- ADMIN: semua. USER: lihat + update dari CKB (hapus hanya ADMIN)
INSERT INTO TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, M.MENU_ID, 'system', GETDATE()
FROM (VALUES ('ADMIN'), ('USER')) R (ROLE_ID)
CROSS JOIN TB_M_MENU M
WHERE M.MENU_ID IN ('M00002', 'M00002-01', 'M00002-02')
  AND NOT EXISTS (SELECT 1 FROM TB_M_AUTH_MENU A WHERE A.ROLE_ID = R.ROLE_ID AND A.MENU_ID = M.MENU_ID);

INSERT INTO TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT X.ROLE_ID, X.FUNCTION_ID, 'system', GETDATE()
FROM (VALUES
	('ADMIN', 'PARTS-SYNC'), ('ADMIN', 'PARTS-DELETE'), ('ADMIN', 'SUPPLIER-SYNC'), ('ADMIN', 'SUPPLIER-DELETE'),
	('USER', 'PARTS-SYNC'), ('USER', 'SUPPLIER-SYNC')
) X (ROLE_ID, FUNCTION_ID)
WHERE NOT EXISTS (SELECT 1 FROM TB_M_AUTH_FUNCTION A WHERE A.ROLE_ID = X.ROLE_ID AND A.FUNCTION_ID = X.FUNCTION_ID);
GO
