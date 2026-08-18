alter table ai_speech_assistant_knowledge
    add column `transfer_call_number` varchar(256) null after `incoming_call_number`;