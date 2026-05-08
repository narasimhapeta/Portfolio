import threading
import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from app.consumer import start_consuming

logging.basicConfig(level=logging.INFO)


@asynccontextmanager
async def lifespan(app: FastAPI):
    thread = threading.Thread(target=start_consuming, daemon=True)
    thread.start()
    yield


app = FastAPI(title="FractureGuard Predictor", lifespan=lifespan)


@app.get("/health")
def health():
    return {"status": "ok"}
