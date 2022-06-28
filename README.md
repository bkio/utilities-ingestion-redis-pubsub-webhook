
# Redis Pub/Sub Webhook

## Required Environment Variables specific to this service:

**TOPIC_ENDPOINT_MAP_FETCH_METHOD**

Value could be either **REDIS** or **ENV_VAR**

If **TOPIC_ENDPOINT_MAP_FETCH_METHOD** is **REDIS**; you must set **REDIS_KEY_TOPICS_TO_BE_LISTENED_MAP**

Else if **TOPIC_ENDPOINT_MAP_FETCH_METHOD** is **ENV_VAR**; you must set **TOPIC_ENDPOINT_MAP**

**REDIS_KEY_TOPICS_TO_BE_LISTENED_MAP**

**Example value:** 

    PUBSUB:WEBHOOK:CONFIG:TOPICS 

There must be THREE delimiters, and delimiters must be :

It is Redis hash key based; in the example; PUBSUB:WEBHOOK:CONFIG is hash key; TOPICS is field.
Therefore; setting the value could be done with;

    HSET PUBSUB:WEBHOOK:CONFIG TOPICS "{ \"topicName\":[{\"endpoint...."

Value expanded:

     { 
        "topicName":
        [
         {
            "endpoint": "",
            "headers": [{
              "key": "",
              "values": [ "", "" ]
            }],
            "verb": "GET/POST/PUT/DELETE"
         }  
        ]
       }
    
**TOPIC_ENDPOINT_MAP**

**Example value:** 
(Same with "Value expanded:" part above under REDIS_KEY_TOPICS_TO_BE_LISTENED_MAP)

      { 
        "topicName":
        [
         {
            "endpoint": "",
            "headers": [{
              "key": "",
              "values": [ "", "" ]
            }],
            "verb": "GET/POST/PUT/DELETE"
         }  
        ]
       }


**REDIS_KEY_PREFIX_ACK**

**Example value:**

    PUBSUB:WEBHOOK_ACK 

There must be ONE delimiter, and : must be the it.

## Other environment variables required:
    PORT
    PROGRAM_ID
    DEPLOYMENT_BRANCH_NAME
    DEPLOYMENT_BUILD_NUMBER
    REDIS_ENDPOINT
    REDIS_PORT
    REDIS_PASSWORD
    REDIS_SSL_ENABLED