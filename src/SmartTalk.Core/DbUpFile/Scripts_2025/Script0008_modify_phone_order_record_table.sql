INSERT INTO agent (relate_id, type, created_date)
SELECT id, 0, NOW()
FROM restaurant;

UPDATE phone_order_record AS record
SET record.agent_id = (
    SELECT A.id
    FROM agent AS A JOIN restaurant AS R ON A.relate_id = R.id
    WHERE R.name = '福满楼'
    LIMIT 1
)
WHERE record.restaurant = 0;

UPDATE phone_order_record AS record
SET record.agent_id = (
    SELECT A.id
    FROM agent AS A JOIN restaurant AS R ON A.relate_id = R.id
    WHERE R.name = '江南春'
    LIMIT 1
)
WHERE record.restaurant = 1;

UPDATE phone_order_record AS record
SET record.agent_id = (
    SELECT A.id
    FROM agent AS A JOIN restaurant AS R ON A.relate_id = R.id
    WHERE R.name = '湘潭人家'
    LIMIT 1
)
WHERE record.restaurant = 2;

UPDATE phone_order_record AS record
SET record.agent_id = (
    SELECT A.id
    FROM agent AS A JOIN restaurant AS R ON A.relate_id = R.id
    WHERE R.name = '悟空'
    LIMIT 1
)
WHERE record.restaurant = 3;

alter table `phone_order_record` drop column `restaurant`;


