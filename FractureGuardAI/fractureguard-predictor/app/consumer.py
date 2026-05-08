import json
import logging
import os
import time
import pika
from app.models import AnalysisRequest
from app.predictor import predict_screen_out

logger = logging.getLogger(__name__)

RABBITMQ_HOST = os.getenv("RABBITMQ_HOST", "localhost")
RABBITMQ_USER = os.getenv("RABBITMQ_USER", "guest")
RABBITMQ_PASS = os.getenv("RABBITMQ_PASS", "guest")
REQUEST_QUEUE  = "analysis-requests"
RESULT_QUEUE   = "analysis-results"


def handle_message(channel, method, properties, body: bytes) -> None:
    try:
        payload = json.loads(body)
        request = AnalysisRequest(**payload)
        result = predict_screen_out(request.sensor_snapshot, session_id=request.session_id)

        channel.basic_publish(
            exchange="",
            routing_key=RESULT_QUEUE,
            body=result.model_dump_json(),
            properties=pika.BasicProperties(content_type="application/json"),
        )
        channel.basic_ack(delivery_tag=method.delivery_tag)
        logger.info("Published result for session %s risk=%.1f%%", result.session_id, result.risk_pct)
    except Exception:
        logger.exception("Failed to process message")
        channel.basic_nack(delivery_tag=method.delivery_tag, requeue=False)


def start_consuming() -> None:
    while True:
        try:
            credentials = pika.PlainCredentials(RABBITMQ_USER, RABBITMQ_PASS)
            conn = pika.BlockingConnection(
                pika.ConnectionParameters(host=RABBITMQ_HOST, credentials=credentials)
            )
            ch = conn.channel()
            ch.queue_declare(queue=REQUEST_QUEUE, durable=True)
            ch.queue_declare(queue=RESULT_QUEUE, durable=True)
            ch.basic_qos(prefetch_count=1)
            ch.basic_consume(queue=REQUEST_QUEUE, on_message_callback=handle_message)
            logger.info("Waiting for analysis requests on %s", REQUEST_QUEUE)
            ch.start_consuming()
        except pika.exceptions.AMQPConnectionError:
            logger.warning("RabbitMQ not ready, retrying in 5s...")
            time.sleep(5)
