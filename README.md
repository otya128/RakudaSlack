# RakudaSlack

# Example
```bas
basic:LET VOID$=CALL("onposted", MAKEPROCINSTANCE "@ONPOSTED")
LET VOID$=CALL("onreaction", MAKEPROCINSTANCE "@ONREACTION")
LET VOID$=CALL("onreactionremoved", MAKEPROCINSTANCE "@ONREACTION")
?"ｼﾞｬﾝｹﾝ"
GOTO@END
@ONPOSTED
VOID$=CALL("addreaction", "facepunch")
VOID$=CALL("addreaction", "v")
VOID$=CALL("addreaction", "hand")
GOTO@END
@GETHAND
IF A1$="facepunch" THEN HAND=0
IF A1$="v" THEN HAND=1
IF A1$="hand" THEN HAND=2
RETURN
@ONREACTION
?A2$,":";A1$;":"
LET MYHAND=INT(RND()*3)
IF MYHAND=0 THEN ?":facepunch:"
IF MYHAND=1 THEN ?":v:"
IF MYHAND=2 THEN ?":hand:"
GOSUB@GETHAND
IF HAND=MYHAND THEN ?"あいこ"
IF (HAND-MYHAND +3)%3=2 THEN ?"勝ち"
IF (HAND-MYHAND +3)%3=1 THEN ?"負け"
GOTO@END
@END
```
