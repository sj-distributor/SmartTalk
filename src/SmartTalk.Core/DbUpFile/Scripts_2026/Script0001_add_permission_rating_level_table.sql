CREATE TABLE  IF NOT EXISTS `permission_rating_level`(
    `id` INT NOT NULL AUTO_INCREMENT,
    `permission_id` INT NOT NULL,
    `permission_level` INT NOT NULL,

    PRIMARY KEY (`id`),

    INDEX `idx_permission_id` (`permission_id`),
    INDEX `idx_permission_level` (`permission_level`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO permission_rating_level (permission_id, permission_level)
SELECT p.id, 1
FROM permission p
WHERE p.name IN (
                 'CanViewPhoneOrder',
                 'CanViewAutoCall',
                 'CanViewKnowledge',
                 'CanViewMerchPrinter',
                 'CanViewAiAgent',
                 'CanViewDataDashboard'
    );

INSERT INTO permission_rating_level (permission_id, permission_level)
SELECT p.id, 0
FROM permission p
WHERE p.name IN (
                 'CanViewPhoneOrder',
                 'CanViewAccountManagement',
                 'CanCreateAccount',
                 'CanDeleteAccount',
                 'CanCopyAccount',
                 'CanUpdateAccount',
                 'CanViewPlaceOrder',
                 'CanViewBusinessManagement',
                 'CanViewDataDashboard',
                 'CanViewAutoTest'
    );