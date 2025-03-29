from typing import Optional
from pydantic import BaseModel


# RuleTo is the model for a normalized rule (containing DB IDs)
# does not contain the rule_to_id primary key as this one is set by the database
class RuleTo(BaseModel):
    active: bool = True
    rule_id: int
    obj_id: int
    rt_create: int
    rt_last_seen: int
    removed: Optional[int] = None
    user_id: Optional[int] = None
    negated: bool = False
