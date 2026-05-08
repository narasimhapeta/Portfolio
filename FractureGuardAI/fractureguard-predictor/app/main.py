import threading
import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI, HTTPException
from app.consumer import start_consuming

logging.basicConfig(level=logging.INFO)
_consumer_thread: threading.Thread | None = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global _consumer_thread
    _consumer_thread = threading.Thread(
        target=start_consuming, daemon=True, name="rabbitmq-consumer"
    )
    _consumer_thread.start()
    yield


app = FastAPI(title="FractureGuard Predictor", lifespan=lifespan)


@app.get("/health")
def health():
    if _consumer_thread is None or not _consumer_thread.is_alive():
        raise HTTPException(status_code=503, detail="consumer thread is not running")
    return {"status": "ok"}
