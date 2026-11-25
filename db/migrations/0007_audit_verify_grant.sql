-- Worker, audit zincirini saatlik doğrular (AuditChainAnchorJob).
-- 0006 uygulanmış olduğu için değiştirilmez; eksik yetki yeni script ile eklenir.
GRANT EXECUTE ON ops.usp_VerifyAuditChain TO zr_worker_role;
