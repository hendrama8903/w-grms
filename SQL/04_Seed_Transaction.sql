-- =====================================================================
-- W-GRMS : menu & function Transaction (CKB)
--   Order Data (CKB)              -> OLKKBNORD (download dari database CKB plant)
--   Goods Receiving (CKB)         -> TB_T_RECEIVE_H / TB_T_RECEIVE_D (+ DN sisa di OLKKBNORD)
--   Delivery Note Printing (CKB)  -> cetak DN sisa (DN#n)
--   Data SOC (CKB)                -> OLKSOC langsung dari database CKB plant
-- Koneksi CKB per plant ada di appsettings.json "CkbSources".
--
-- Jalankan di database GRMS (aman dijalankan ulang)
-- =====================================================================

MERGE TB_M_MENU AS T
USING (VALUES
	('M00003',    NULL,     'Transaction',                   'fa-exchange',       '#',                          3),
	('M00003-01', 'M00003', 'Order Data (CKB)',              'fa-cloud-download', '/OrderData/Index',            1),
	('M00003-02', 'M00003', 'Goods Receiving (CKB)',         'fa-inbox',          '/GoodsReceiving/Index',       2),
	('M00003-03', 'M00003', 'Delivery Note Printing (CKB)',  'fa-print',          '/DeliveryNotePrinting/Index', 3),
	('M00003-05', 'M00003', 'Data SOC (CKB)',                'fa-table',          '/DataSoc/Index',              5)
) AS S (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ)
ON T.MENU_ID = S.MENU_ID
WHEN NOT MATCHED THEN
	INSERT (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.MENU_ID, S.PARENT_ID, S.MENU_NAME, S.MENU_ICON, S.MENU_URL, S.MENU_SEQ, 0, 'system', GETDATE());
GO

MERGE TB_M_FUNCTION AS T
USING (VALUES
	('ORDER-DOWNLOAD', 'M00003-01', 'Download', 'Download order data from CKB database'),
	('ORDER-DELETE',   'M00003-01', 'Delete',   'Delete order data that has not been received'),
	('GR-SAVE',        'M00003-02', 'Save',     'Save goods receive'),
	('GR-DELETE',      'M00003-02', 'Delete',   'Delete goods receive')
) AS S (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC)
ON T.FUNCTION_ID = S.FUNCTION_ID
WHEN NOT MATCHED THEN
	INSERT (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, DELETE_FLAG, CREATED_BY, CREATED_DT)
	VALUES (S.FUNCTION_ID, S.MENU_ID, S.FUNCTION_NAME, S.FUNCTION_DESC, 0, 'system', GETDATE());
GO

-- ADMIN: semua menu & function Transaction
-- USER : Order Data (download), Goods Receiving (simpan & hapus), DN Printing, Data SOC.
--        Hapus Order Data hanya ADMIN (di desktop hanya sebagian kecil user yang punya akses ini).
INSERT INTO TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT X.ROLE_ID, X.MENU_ID, 'system', GETDATE()
FROM (VALUES
	('ADMIN', 'M00003'), ('ADMIN', 'M00003-01'), ('ADMIN', 'M00003-02'), ('ADMIN', 'M00003-03'), ('ADMIN', 'M00003-05'),
	('USER',  'M00003'), ('USER',  'M00003-01'), ('USER',  'M00003-02'), ('USER',  'M00003-03'), ('USER',  'M00003-05')
) X (ROLE_ID, MENU_ID)
WHERE NOT EXISTS (SELECT 1 FROM TB_M_AUTH_MENU A WHERE A.ROLE_ID = X.ROLE_ID AND A.MENU_ID = X.MENU_ID);

INSERT INTO TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT X.ROLE_ID, X.FUNCTION_ID, 'system', GETDATE()
FROM (VALUES
	('ADMIN', 'ORDER-DOWNLOAD'), ('ADMIN', 'ORDER-DELETE'), ('ADMIN', 'GR-SAVE'), ('ADMIN', 'GR-DELETE'),
	('USER',  'ORDER-DOWNLOAD'), ('USER',  'GR-SAVE'), ('USER',  'GR-DELETE')
) X (ROLE_ID, FUNCTION_ID)
WHERE NOT EXISTS (SELECT 1 FROM TB_M_AUTH_FUNCTION A WHERE A.ROLE_ID = X.ROLE_ID AND A.FUNCTION_ID = X.FUNCTION_ID);
GO

-- Menu Delivery Note Cleansing (CKB) sudah tidak dipakai - bersihkan bila sempat terpasang
DELETE FROM TB_M_AUTH_FUNCTION WHERE FUNCTION_ID = 'DNCLEAN-DELETE';
DELETE FROM TB_M_FUNCTION WHERE FUNCTION_ID = 'DNCLEAN-DELETE';
DELETE FROM TB_M_AUTH_MENU WHERE MENU_ID = 'M00003-04';
DELETE FROM TB_M_MENU WHERE MENU_ID = 'M00003-04';
GO
