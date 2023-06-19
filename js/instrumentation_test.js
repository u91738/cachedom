
function test() {
    const b = document.getElementsByTagName('body')[0]

    Function('n', 'return n + "test"')

    eval('"eval" + "()"')

    document.write('<b>test</b>')
    document.writeln('<b>test</b>')

    const el = document.createElement('i')
    el.innerHTML = 'el.prepend'
    b.prepend(el)

    b.addEventListener('onload', function(){})

    b.insertAdjacentHTML('afterbegin','<i>HTMLElement_insertAdjacentHTML</i>')

    el.innerHTML = 'el.insertAdjacentElement'
    b.insertAdjacentElement('afterbegin', el)

    b.onload = 'console.log("HTMLElement_addEventListener")'

    el.innerHTML = 'el.appendChild'
    b.appendChild(el)

    const el2 = document.createElement('b')
    el2.innerHTML = 'el.replaceChild'
    b.replaceChild(el2, el)

    b.setAttribute('style', 'el.setAttribute')

    b.innerHTML = '<i>innerHTML</i>'
    b.outerHTML = '<body><i>outerHTML</i></body>'

    script = document.createElement('script')
    script.src = 'script_src'

    form = document.createElement('form')
    form.action = 'form_action'

    butt = document.createElement('button')
    butt.formAction = 'button_formAction'

    setInterval(0, 'setInterval')

    setTimeout(0, 'setTimeout')

    return script_instrumentation_result
}

test()
