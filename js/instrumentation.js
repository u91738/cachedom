// collect data that gets into executable places
try {
    script_instrumentation_result
} catch(ReferenceError) {
    script_instrumentation_result = {
        eval : [],
        setTimeout: [],
        setInterval: [],
        Function : [],
        document_write : [],
        document_writeln : [],
        HTMLElement_addEventListener : [],
        HTMLElement_insertAdjacentHTML : [],
        HTMLElement_setAttribute : [],
        HTMLElement_innerHTML_set : [],
        HTMLElement_outerHTML_set : [],
        HTMLScriptElement_src_set : [],
        HTMLInputElement_formAction_set : [],
        HTMLFormElement_action_set : [],
    }

    function to_text_simple(obj, force) {
        var oh = undefined
        if(typeof(obj) === 'string' || typeof(obj) === 'number' || obj === undefined || obj === null) {
            return String(obj)
        }
        try {
            oh = obj.outerHTML
        } catch {}
        if(oh === undefined) {
            try {
                oh = obj.innerHTML
            } catch { }
        }
        if(oh === undefined) {
            try {
                oh = JSON.stringify(obj)
            } catch { }
        }
        if(oh === undefined && force) {
            oh = String(obj)
        }
        return oh
    }

    function to_text(o, depth) {
        const rs = to_text_simple(o, false)
        if(rs !== undefined) {
            return rs
        } else {
            var r = []
            if(depth < 3) {
                for(i in o)
                    r.push(i + ' : ' + to_text(o[i], depth + 1))
            } else {
                for(i in o)
                    r.push(i + ' : ' + to_text_simple(o, true))
            }
            return r.join(',\u0013')
        }
    }

    function obj_desc(obj) {
        r =[]
        for(i=0; i < obj.length; ++i)
            r.push(to_text(obj[i], 0))
        return { stack: new Error().stack, args: r }
    }

    const old_eval = eval
    eval = function() {
        script_instrumentation_result.eval.push(obj_desc(arguments))
        return old_eval.apply(this, arguments)
    }

    const old_setTimeout = setTimeout
    setTimeout = function() {
        script_instrumentation_result.setTimeout.push(obj_desc(arguments))
        return old_setTimeout.apply(this, arguments)
    }

    const old_setInterval = setInterval
    setInterval = function() {
        script_instrumentation_result.setInterval.push(obj_desc(arguments))
        return old_setInterval.apply(this, arguments)
    }

    const old_Function = Function
    Function = function() {
        script_instrumentation_result.Function.push(obj_desc(arguments))
        return old_Function.apply(this, arguments)
    }

    function script_instr_wrap_method(obj, obj_field, res_field) {
        const old = obj[obj_field]
        obj[obj_field] = function() {
            script_instrumentation_result[res_field].push(obj_desc(arguments))
            old.apply(this, arguments)
        }
    }

    function script_instr_wrap_setter(obj, obj_field, res_field) {
        const oldS = obj.__lookupSetter__(obj_field)
        const oldG = obj.__lookupGetter__(obj_field)
        obj.__defineSetter__(obj_field, function() {
            script_instrumentation_result[res_field].push(obj_desc(arguments))
            return oldS.apply(this, arguments)
        })
        if(typeof oldG !== 'undefined') {
            obj.__defineGetter__(obj_field, oldG)
        }
    }

    script_instr_wrap_method(document, 'write', 'document_write')
    script_instr_wrap_method(document, 'writeln', 'document_writeln')
    script_instr_wrap_method(HTMLElement.prototype, 'insertAdjacentHTML', 'HTMLElement_insertAdjacentHTML')
    script_instr_wrap_method(HTMLElement.prototype, 'addEventListener', 'HTMLElement_addEventListener')
    script_instr_wrap_method(HTMLElement.prototype, 'setAttribute', 'HTMLElement_setAttribute')
    script_instr_wrap_setter(HTMLElement.prototype, 'innerHTML', 'HTMLElement_innerHTML_set')
    script_instr_wrap_setter(HTMLElement.prototype, 'outerHTML', 'HTMLElement_outerHTML_set')
    script_instr_wrap_setter(HTMLScriptElement.prototype, 'src', 'HTMLScriptElement_src_set')
    script_instr_wrap_setter(HTMLInputElement.prototype, 'formAction', 'HTMLInputElement_formAction_set')
    script_instr_wrap_setter(HTMLButtonElement.prototype, 'formAction', 'HTMLInputElement_formAction_set')
    script_instr_wrap_setter(HTMLFormElement.prototype, 'action', 'HTMLFormElement_action_set')
}
