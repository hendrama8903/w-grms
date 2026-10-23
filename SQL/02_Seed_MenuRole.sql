-- =====================================================================
-- W-GRMS : data awal role, menu, function & otorisasi
-- Menu bisnis (Master/Transaction/Report/SAP Interface) ditambahkan
-- per modul saat modulnya selesai dibuat.
--
-- Jalankan di database GRMS (aman dijalankan ulang)
-- =====================================================================

-- Role
IF NOT EXISTS (SELECT 1 FROM TB_M_ROLE WHERE ROLE_ID = 'ADMIN')
	INSERT INTO TB_M_ROLE (ROLE_ID, ROLE_NAME, ROLE_DESC, CREATED_BY, CREATED_DT)
	VALUES ('ADMIN', 'Administrator', 'Full access to all GRMS menus', 'system', GETDATE());

IF NOT EXISTS (SELECT 1 FROM TB_M_ROLE WHERE ROLE_ID = 'USER')
	INSERT INTO TB_M_ROLE (ROLE_ID, ROLE_NAME, ROLE_DESC, CREATED_BY, CREATED_DT)
	VALUES ('USER', 'GRMS User', 'Operational user (Goods Receiving)', 'system', GETDATE());
GO

-- Menu
MERGE TB_M_MENU AS T
USING (VALUES
	('M00001',    NULL,     'Dashboard',          'fa-dashboard', '/Home/Index',          1),
	('M00009',    NULL,     'System',             'fa-cogs',      '#',                    9),
	('M00009-01', 'M00009', 'User Management',    'fa-users',     '/User/Index',          1),
	('M00009-02', 'M00009', 'Role Authorization', 'fa-key',       '/Role/Index',          2),
	('M00009-03', 'M00009', 'Menu & Function',    'fa-sitemap',   '/Menu/Index',          3),
	('M00009-04', 'M00009', 'Activity Log',       'fa-history',   '/LogMonitoring/Index', 4)
) AS S (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ)
ON T.MENU_ID = S.MENU_ID
WHEN NOT MATCHED THEN
	INSERT (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.MENU_ID, S.PARENT_ID, S.MENU_NAME, S.MENU_ICON, S.MENU_URL, S.MENU_SEQ, 0, 'system', GETDATE());
GO

-- Function
MERGE TB_M_FUNCTION AS T
USING (VALUES
	('USER-ADD',           'M00009-01', 'Add',           'Add user'),
	('USER-EDIT',          'M00009-01', 'Edit',          'Edit user'),
	('USER-DELETE',        'M00009-01', 'Delete',        'Delete user'),
	('USER-RESTORE',       'M00009-01', 'Restore',       'Restore deleted user'),
	('ROLE-ADD',           'M00009-02', 'Add',           'Add role'),
	('ROLE-EDIT',          'M00009-02', 'Edit',          'Edit role'),
	('ROLE-DELETE',        'M00009-02', 'Delete',        'Delete role'),
	('ROLE-AUTHORIZATION', 'M00009-02', 'Authorization', 'Set role menu & function access'),
	('MENU-ADD',           'M00009-03', 'Add',           'Add menu'),
	('MENU-EDIT',          'M00009-03', 'Edit',          'Edit menu'),
	('MENU-DELETE',        'M00009-03', 'Delete',        'Delete menu'),
	('MENU-FUNCTION',      'M00009-03', 'Function',      'Maintain menu functions')
) AS S (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC)
ON T.FUNCTION_ID = S.FUNCTION_ID
WHEN NOT MATCHED THEN
	INSERT (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.FUNCTION_ID, S.MENU_ID, S.FUNCTION_NAME, S.FUNCTION_DESC, 0, 'system', GETDATE());
GO

-- ADMIN: semua menu & function
INSERT INTO TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT 'ADMIN', M.MENU_ID, 'system', GETDATE()
FROM TB_M_MENU M
WHERE NOT EXISTS (SELECT 1 FROM TB_M_AUTH_MENU A WHERE A.ROLE_ID = 'ADMIN' AND A.MENU_ID = M.MENU_ID);

INSERT INTO TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT 'ADMIN', F.FUNCTION_ID, 'system', GETDATE()
FROM TB_M_FUNCTION F
WHERE NOT EXISTS (SELECT 1 FROM TB_M_AUTH_FUNCTION A WHERE A.ROLE_ID = 'ADMIN' AND A.FUNCTION_ID = F.FUNCTION_ID);

-- USER: dashboard
IF NOT EXISTS (SELECT 1 FROM TB_M_AUTH_MENU WHERE ROLE_ID = 'USER' AND MENU_ID = 'M00001')
	INSERT INTO TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
	VALUES ('USER', 'M00001', 'system', GETDATE());
GO

-- User lama: IT Application = ADMIN, sisanya USER
UPDATE TB_M_USER
SET ROLE_ID = CASE WHEN DEPT LIKE 'IT Application%' THEN 'ADMIN' ELSE 'USER' END
WHERE ROLE_ID IS NULL;
GO
