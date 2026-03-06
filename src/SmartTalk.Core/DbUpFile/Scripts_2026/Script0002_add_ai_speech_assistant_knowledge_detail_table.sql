CREATE TABLE IF NOT EXISTS ai_speech_assistant_knowledge_detail (
  `id` INT NOT NULL AUTO_INCREMENT,
  `knowledge_id` INT NOT NULL,
  `knowledge_name` VARCHAR(255) NOT NULL,
  `format_type` INT NOT NULL,
  `content` TEXT NOT NULL,
  `created_date` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `last_modified_by` INT NULL,
  `last_modified_date` DATETIME(6) NULL DEFAULT NULL,
  PRIMARY KEY (`id`),
  INDEX `idx_knowledge_id` (`knowledge_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;1

INSERT INTO ai_speech_assistant_knowledge_detail
(
    knowledge_id,
    knowledge_name,
    format_type,
    content,
    created_date,
    lastModified_date
)
SELECT
    k.id,
    j.key_name,
    0,
    jt.value,
    NOW(),
    NOW()
FROM ai_speech_assistant_knowledge k
         JOIN JSON_TABLE(
        JSON_KEYS(k.json),
        '$[*]' COLUMNS (
        key_name VARCHAR(255) PATH '$'
    )
              ) j
         JOIN JSON_TABLE(
        JSON_EXTRACT(k.json, CONCAT('$.', j.key_name)),
        '$[*]' COLUMNS (
        value TEXT PATH '$'
    )
              ) jt
WHERE k.json IS NOT NULL
  AND k.json <> '{}';