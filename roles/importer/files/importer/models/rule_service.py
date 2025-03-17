from typing import Optional
from pydantic import BaseModel


class RuleService(BaseModel):
    active: bool = True
    rule_id: int
    svc_id: int
    rs_create: int
    rs_last_seen: int
    removed: Optional[int] = None
    negated: bool = False