grammar LabCalculator;

compileUnit : expression EOF;

expression : logicEqv;

// Пріоритети (від найнижчого до найвищого): eqv, or, and, not, порівняння, + -, * /
logicEqv       : logicOr (EQV logicOr)*;
logicOr        : logicAnd (OR logicAnd)*;
logicAnd       : logicNot (AND logicNot)*;
logicNot       : NOT logicNot | comparison;
comparison     : additive ((EQ | LT | GT) additive)?;
additive       : multiplicative ((PLUS | MINUS) multiplicative)*;
multiplicative : atom ((MUL | DIV) atom)*;

atom
    : NUMBER
    | IDENTIFIER
    | TRUE
    | FALSE
    | LPAREN expression RPAREN
    ;

EQV    : 'eqv';
OR     : 'or';
AND    : 'and';
NOT    : 'not';
EQ     : '=';
LT     : '<';
GT     : '>';
PLUS   : '+';
MINUS  : '-';
MUL    : '*';
DIV    : '/';
LPAREN : '(';
RPAREN : ')';
TRUE   : 'true';
FALSE  : 'false';

NUMBER     : [0-9]+;
IDENTIFIER : [a-z]+ [0-9]+;
WS         : [ \t\r\n]+ -> skip;

// Усе, що не розпізнано вище, стає токеном UNKNOWN, і парсер повідомляє про нього як про синтаксичну помилку
UNKNOWN : [a-z]+ | . ;
