UPDATE `ai_speech_assistant_knowledge` knowledge
    JOIN `ai_speech_assistant` assistant ON assistant.`id` = knowledge.`assistant_id`
    SET knowledge.`model_language` = assistant.`model_language`
WHERE assistant.`model_language` IS NOT NULL;