import{c as i,o,a as u,m as r,B as O,r as p,b as f,d as A,t as T,n as M,e as L,f as K,g as H,w,h as C,v as W,T as F,i as J,s as d,j as q,k as R,l as G,p as z,u as g,q as Q,F as X,x as Z,y as ee}from"./index-B-iLH0y4.js";import{s as ne,a as S,f as U,R as se,b as te}from"./index-BvJtBZZu.js";var Y={name:"TimesIcon",extends:ne};function ae(e){return le(e)||ie(e)||re(e)||oe()}function oe(){throw new TypeError(`Invalid attempt to spread non-iterable instance.
In order to be iterable, non-array objects must have a [Symbol.iterator]() method.`)}function re(e,n){if(e){if(typeof e=="string")return _(e,n);var s={}.toString.call(e).slice(8,-1);return s==="Object"&&e.constructor&&(s=e.constructor.name),s==="Map"||s==="Set"?Array.from(e):s==="Arguments"||/^(?:Ui|I)nt(?:8|16|32)(?:Clamped)?Array$/.test(s)?_(e,n):void 0}}function ie(e){if(typeof Symbol<"u"&&e[Symbol.iterator]!=null||e["@@iterator"]!=null)return Array.from(e)}function le(e){if(Array.isArray(e))return _(e)}function _(e,n){(n==null||n>e.length)&&(n=e.length);for(var s=0,t=Array(n);s<n;s++)t[s]=e[s];return t}function ce(e,n,s,t,l,a){return o(),i("svg",r({width:"14",height:"14",viewBox:"0 0 14 14",fill:"none",xmlns:"http://www.w3.org/2000/svg"},e.pti()),ae(n[0]||(n[0]=[u("path",{d:"M8.01186 7.00933L12.27 2.75116C12.341 2.68501 12.398 2.60524 12.4375 2.51661C12.4769 2.42798 12.4982 2.3323 12.4999 2.23529C12.5016 2.13827 12.4838 2.0419 12.4474 1.95194C12.4111 1.86197 12.357 1.78024 12.2884 1.71163C12.2198 1.64302 12.138 1.58893 12.0481 1.55259C11.9581 1.51625 11.8617 1.4984 11.7647 1.50011C11.6677 1.50182 11.572 1.52306 11.4834 1.56255C11.3948 1.60204 11.315 1.65898 11.2488 1.72997L6.99067 5.98814L2.7325 1.72997C2.59553 1.60234 2.41437 1.53286 2.22718 1.53616C2.03999 1.53946 1.8614 1.61529 1.72901 1.74767C1.59663 1.88006 1.5208 2.05865 1.5175 2.24584C1.5142 2.43303 1.58368 2.61419 1.71131 2.75116L5.96948 7.00933L1.71131 11.2675C1.576 11.403 1.5 11.5866 1.5 11.7781C1.5 11.9696 1.576 12.1532 1.71131 12.2887C1.84679 12.424 2.03043 12.5 2.2219 12.5C2.41338 12.5 2.59702 12.424 2.7325 12.2887L6.99067 8.03052L11.2488 12.2887C11.3843 12.424 11.568 12.5 11.7594 12.5C11.9509 12.5 12.1346 12.424 12.27 12.2887C12.4053 12.1532 12.4813 11.9696 12.4813 11.7781C12.4813 11.5866 12.4053 11.403 12.27 11.2675L8.01186 7.00933Z",fill:"currentColor"},null,-1)])),16)}Y.render=ce;var de=`
    .p-avatar {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: dt('avatar.width');
        height: dt('avatar.height');
        font-size: dt('avatar.font.size');
        background: dt('avatar.background');
        color: dt('avatar.color');
        border-radius: dt('avatar.border.radius');
    }

    .p-avatar-image {
        background: transparent;
    }

    .p-avatar-circle {
        border-radius: 50%;
    }

    .p-avatar-circle img {
        border-radius: 50%;
    }

    .p-avatar-icon {
        font-size: dt('avatar.icon.size');
        width: dt('avatar.icon.size');
        height: dt('avatar.icon.size');
    }

    .p-avatar img {
        width: 100%;
        height: 100%;
    }

    .p-avatar-lg {
        width: dt('avatar.lg.width');
        height: dt('avatar.lg.width');
        font-size: dt('avatar.lg.font.size');
    }

    .p-avatar-lg .p-avatar-icon {
        font-size: dt('avatar.lg.icon.size');
        width: dt('avatar.lg.icon.size');
        height: dt('avatar.lg.icon.size');
    }

    .p-avatar-xl {
        width: dt('avatar.xl.width');
        height: dt('avatar.xl.width');
        font-size: dt('avatar.xl.font.size');
    }

    .p-avatar-xl .p-avatar-icon {
        font-size: dt('avatar.xl.icon.size');
        width: dt('avatar.xl.icon.size');
        height: dt('avatar.xl.icon.size');
    }

    .p-avatar-group {
        display: flex;
        align-items: center;
    }

    .p-avatar-group .p-avatar + .p-avatar {
        margin-inline-start: dt('avatar.group.offset');
    }

    .p-avatar-group .p-avatar {
        border: 2px solid dt('avatar.group.border.color');
    }

    .p-avatar-group .p-avatar-lg + .p-avatar-lg {
        margin-inline-start: dt('avatar.lg.group.offset');
    }

    .p-avatar-group .p-avatar-xl + .p-avatar-xl {
        margin-inline-start: dt('avatar.xl.group.offset');
    }
`,ue={root:function(n){var s=n.props;return["p-avatar p-component",{"p-avatar-image":s.image!=null,"p-avatar-circle":s.shape==="circle","p-avatar-lg":s.size==="large","p-avatar-xl":s.size==="xlarge"}]},label:"p-avatar-label",icon:"p-avatar-icon"},pe=O.extend({name:"avatar",style:de,classes:ue}),me={name:"BaseAvatar",extends:S,props:{label:{type:String,default:null},icon:{type:String,default:null},image:{type:String,default:null},size:{type:String,default:"normal"},shape:{type:String,default:"square"},ariaLabelledby:{type:String,default:null},ariaLabel:{type:String,default:null}},style:pe,provide:function(){return{$pcAvatar:this,$parentInstance:this}}};function v(e){"@babel/helpers - typeof";return v=typeof Symbol=="function"&&typeof Symbol.iterator=="symbol"?function(n){return typeof n}:function(n){return n&&typeof Symbol=="function"&&n.constructor===Symbol&&n!==Symbol.prototype?"symbol":typeof n},v(e)}function N(e,n,s){return(n=ge(n))in e?Object.defineProperty(e,n,{value:s,enumerable:!0,configurable:!0,writable:!0}):e[n]=s,e}function ge(e){var n=be(e,"string");return v(n)=="symbol"?n:n+""}function be(e,n){if(v(e)!="object"||!e)return e;var s=e[Symbol.toPrimitive];if(s!==void 0){var t=s.call(e,n);if(v(t)!="object")return t;throw new TypeError("@@toPrimitive must return a primitive value.")}return(n==="string"?String:Number)(e)}var I={name:"Avatar",extends:me,inheritAttrs:!1,emits:["error"],methods:{onError:function(n){this.$emit("error",n)}},computed:{dataP:function(){return U(N(N({},this.shape,this.shape),this.size,this.size))}}},fe=["aria-labelledby","aria-label","data-p"],ve=["data-p"],ye=["data-p"],he=["src","alt","data-p"];function Ee(e,n,s,t,l,a){return o(),i("div",r({class:e.cx("root"),"aria-labelledby":e.ariaLabelledby,"aria-label":e.ariaLabel},e.ptmi("root"),{"data-p":a.dataP}),[p(e.$slots,"default",{},function(){return[e.label?(o(),i("span",r({key:0,class:e.cx("label")},e.ptm("label"),{"data-p":a.dataP}),T(e.label),17,ve)):e.$slots.icon?(o(),f(L(e.$slots.icon),{key:1,class:M(e.cx("icon"))},null,8,["class"])):e.icon?(o(),i("span",r({key:2,class:[e.cx("icon"),e.icon]},e.ptm("icon"),{"data-p":a.dataP}),null,16,ye)):e.image?(o(),i("img",r({key:3,src:e.image,alt:e.ariaLabel,onError:n[0]||(n[0]=function(){return a.onError&&a.onError.apply(a,arguments)})},e.ptm("image"),{"data-p":a.dataP}),null,16,he)):A("",!0)]})],16,fe)}I.render=Ee;var Te=`
    .p-message {
        border-radius: dt('message.border.radius');
        outline-width: dt('message.border.width');
        outline-style: solid;
    }

    .p-message-content {
        display: flex;
        align-items: center;
        padding: dt('message.content.padding');
        gap: dt('message.content.gap');
        height: 100%;
    }

    .p-message-icon {
        flex-shrink: 0;
    }

    .p-message-close-button {
        display: flex;
        align-items: center;
        justify-content: center;
        flex-shrink: 0;
        margin-inline-start: auto;
        overflow: hidden;
        position: relative;
        width: dt('message.close.button.width');
        height: dt('message.close.button.height');
        border-radius: dt('message.close.button.border.radius');
        background: transparent;
        transition:
            background dt('message.transition.duration'),
            color dt('message.transition.duration'),
            outline-color dt('message.transition.duration'),
            box-shadow dt('message.transition.duration'),
            opacity 0.3s;
        outline-color: transparent;
        color: inherit;
        padding: 0;
        border: none;
        cursor: pointer;
        user-select: none;
    }

    .p-message-close-icon {
        font-size: dt('message.close.icon.size');
        width: dt('message.close.icon.size');
        height: dt('message.close.icon.size');
    }

    .p-message-close-button:focus-visible {
        outline-width: dt('message.close.button.focus.ring.width');
        outline-style: dt('message.close.button.focus.ring.style');
        outline-offset: dt('message.close.button.focus.ring.offset');
    }

    .p-message-info {
        background: dt('message.info.background');
        outline-color: dt('message.info.border.color');
        color: dt('message.info.color');
        box-shadow: dt('message.info.shadow');
    }

    .p-message-info .p-message-close-button:focus-visible {
        outline-color: dt('message.info.close.button.focus.ring.color');
        box-shadow: dt('message.info.close.button.focus.ring.shadow');
    }

    .p-message-info .p-message-close-button:hover {
        background: dt('message.info.close.button.hover.background');
    }

    .p-message-info.p-message-outlined {
        color: dt('message.info.outlined.color');
        outline-color: dt('message.info.outlined.border.color');
    }

    .p-message-info.p-message-simple {
        color: dt('message.info.simple.color');
    }

    .p-message-success {
        background: dt('message.success.background');
        outline-color: dt('message.success.border.color');
        color: dt('message.success.color');
        box-shadow: dt('message.success.shadow');
    }

    .p-message-success .p-message-close-button:focus-visible {
        outline-color: dt('message.success.close.button.focus.ring.color');
        box-shadow: dt('message.success.close.button.focus.ring.shadow');
    }

    .p-message-success .p-message-close-button:hover {
        background: dt('message.success.close.button.hover.background');
    }

    .p-message-success.p-message-outlined {
        color: dt('message.success.outlined.color');
        outline-color: dt('message.success.outlined.border.color');
    }

    .p-message-success.p-message-simple {
        color: dt('message.success.simple.color');
    }

    .p-message-warn {
        background: dt('message.warn.background');
        outline-color: dt('message.warn.border.color');
        color: dt('message.warn.color');
        box-shadow: dt('message.warn.shadow');
    }

    .p-message-warn .p-message-close-button:focus-visible {
        outline-color: dt('message.warn.close.button.focus.ring.color');
        box-shadow: dt('message.warn.close.button.focus.ring.shadow');
    }

    .p-message-warn .p-message-close-button:hover {
        background: dt('message.warn.close.button.hover.background');
    }

    .p-message-warn.p-message-outlined {
        color: dt('message.warn.outlined.color');
        outline-color: dt('message.warn.outlined.border.color');
    }

    .p-message-warn.p-message-simple {
        color: dt('message.warn.simple.color');
    }

    .p-message-error {
        background: dt('message.error.background');
        outline-color: dt('message.error.border.color');
        color: dt('message.error.color');
        box-shadow: dt('message.error.shadow');
    }

    .p-message-error .p-message-close-button:focus-visible {
        outline-color: dt('message.error.close.button.focus.ring.color');
        box-shadow: dt('message.error.close.button.focus.ring.shadow');
    }

    .p-message-error .p-message-close-button:hover {
        background: dt('message.error.close.button.hover.background');
    }

    .p-message-error.p-message-outlined {
        color: dt('message.error.outlined.color');
        outline-color: dt('message.error.outlined.border.color');
    }

    .p-message-error.p-message-simple {
        color: dt('message.error.simple.color');
    }

    .p-message-secondary {
        background: dt('message.secondary.background');
        outline-color: dt('message.secondary.border.color');
        color: dt('message.secondary.color');
        box-shadow: dt('message.secondary.shadow');
    }

    .p-message-secondary .p-message-close-button:focus-visible {
        outline-color: dt('message.secondary.close.button.focus.ring.color');
        box-shadow: dt('message.secondary.close.button.focus.ring.shadow');
    }

    .p-message-secondary .p-message-close-button:hover {
        background: dt('message.secondary.close.button.hover.background');
    }

    .p-message-secondary.p-message-outlined {
        color: dt('message.secondary.outlined.color');
        outline-color: dt('message.secondary.outlined.border.color');
    }

    .p-message-secondary.p-message-simple {
        color: dt('message.secondary.simple.color');
    }

    .p-message-contrast {
        background: dt('message.contrast.background');
        outline-color: dt('message.contrast.border.color');
        color: dt('message.contrast.color');
        box-shadow: dt('message.contrast.shadow');
    }

    .p-message-contrast .p-message-close-button:focus-visible {
        outline-color: dt('message.contrast.close.button.focus.ring.color');
        box-shadow: dt('message.contrast.close.button.focus.ring.shadow');
    }

    .p-message-contrast .p-message-close-button:hover {
        background: dt('message.contrast.close.button.hover.background');
    }

    .p-message-contrast.p-message-outlined {
        color: dt('message.contrast.outlined.color');
        outline-color: dt('message.contrast.outlined.border.color');
    }

    .p-message-contrast.p-message-simple {
        color: dt('message.contrast.simple.color');
    }

    .p-message-text {
        font-size: dt('message.text.font.size');
        font-weight: dt('message.text.font.weight');
    }

    .p-message-icon {
        font-size: dt('message.icon.size');
        width: dt('message.icon.size');
        height: dt('message.icon.size');
    }

    .p-message-enter-from {
        opacity: 0;
    }

    .p-message-enter-active {
        transition: opacity 0.3s;
    }

    .p-message.p-message-leave-from {
        max-height: 1000px;
    }

    .p-message.p-message-leave-to {
        max-height: 0;
        opacity: 0;
        margin: 0;
    }

    .p-message-leave-active {
        overflow: hidden;
        transition:
            max-height 0.45s cubic-bezier(0, 1, 0, 1),
            opacity 0.3s,
            margin 0.3s;
    }

    .p-message-leave-active .p-message-close-button {
        opacity: 0;
    }

    .p-message-sm .p-message-content {
        padding: dt('message.content.sm.padding');
    }

    .p-message-sm .p-message-text {
        font-size: dt('message.text.sm.font.size');
    }

    .p-message-sm .p-message-icon {
        font-size: dt('message.icon.sm.size');
        width: dt('message.icon.sm.size');
        height: dt('message.icon.sm.size');
    }

    .p-message-sm .p-message-close-icon {
        font-size: dt('message.close.icon.sm.size');
        width: dt('message.close.icon.sm.size');
        height: dt('message.close.icon.sm.size');
    }

    .p-message-lg .p-message-content {
        padding: dt('message.content.lg.padding');
    }

    .p-message-lg .p-message-text {
        font-size: dt('message.text.lg.font.size');
    }

    .p-message-lg .p-message-icon {
        font-size: dt('message.icon.lg.size');
        width: dt('message.icon.lg.size');
        height: dt('message.icon.lg.size');
    }

    .p-message-lg .p-message-close-icon {
        font-size: dt('message.close.icon.lg.size');
        width: dt('message.close.icon.lg.size');
        height: dt('message.close.icon.lg.size');
    }

    .p-message-outlined {
        background: transparent;
        outline-width: dt('message.outlined.border.width');
    }

    .p-message-simple {
        background: transparent;
        outline-color: transparent;
        box-shadow: none;
    }

    .p-message-simple .p-message-content {
        padding: dt('message.simple.content.padding');
    }

    .p-message-outlined .p-message-close-button:hover,
    .p-message-simple .p-message-close-button:hover {
        background: transparent;
    }
`,we={root:function(n){var s=n.props;return["p-message p-component p-message-"+s.severity,{"p-message-outlined":s.variant==="outlined","p-message-simple":s.variant==="simple","p-message-sm":s.size==="small","p-message-lg":s.size==="large"}]},content:"p-message-content",icon:"p-message-icon",text:"p-message-text",closeButton:"p-message-close-button",closeIcon:"p-message-close-icon"},Ae=O.extend({name:"message",style:Te,classes:we}),_e={name:"BaseMessage",extends:S,props:{severity:{type:String,default:"info"},closable:{type:Boolean,default:!1},life:{type:Number,default:null},icon:{type:String,default:void 0},closeIcon:{type:String,default:void 0},closeButtonProps:{type:null,default:null},size:{type:String,default:null},variant:{type:String,default:null}},style:Ae,provide:function(){return{$pcMessage:this,$parentInstance:this}}};function y(e){"@babel/helpers - typeof";return y=typeof Symbol=="function"&&typeof Symbol.iterator=="symbol"?function(n){return typeof n}:function(n){return n&&typeof Symbol=="function"&&n.constructor===Symbol&&n!==Symbol.prototype?"symbol":typeof n},y(e)}function $(e,n,s){return(n=Oe(n))in e?Object.defineProperty(e,n,{value:s,enumerable:!0,configurable:!0,writable:!0}):e[n]=s,e}function Oe(e){var n=Se(e,"string");return y(n)=="symbol"?n:n+""}function Se(e,n){if(y(e)!="object"||!e)return e;var s=e[Symbol.toPrimitive];if(s!==void 0){var t=s.call(e,n);if(y(t)!="object")return t;throw new TypeError("@@toPrimitive must return a primitive value.")}return(n==="string"?String:Number)(e)}var B={name:"Message",extends:_e,inheritAttrs:!1,emits:["close","life-end"],timeout:null,data:function(){return{visible:!0}},mounted:function(){var n=this;this.life&&setTimeout(function(){n.visible=!1,n.$emit("life-end")},this.life)},methods:{close:function(n){this.visible=!1,this.$emit("close",n)}},computed:{closeAriaLabel:function(){return this.$primevue.config.locale.aria?this.$primevue.config.locale.aria.close:void 0},dataP:function(){return U($($({outlined:this.variant==="outlined",simple:this.variant==="simple"},this.severity,this.severity),this.size,this.size))}},directives:{ripple:se},components:{TimesIcon:Y}};function h(e){"@babel/helpers - typeof";return h=typeof Symbol=="function"&&typeof Symbol.iterator=="symbol"?function(n){return typeof n}:function(n){return n&&typeof Symbol=="function"&&n.constructor===Symbol&&n!==Symbol.prototype?"symbol":typeof n},h(e)}function D(e,n){var s=Object.keys(e);if(Object.getOwnPropertySymbols){var t=Object.getOwnPropertySymbols(e);n&&(t=t.filter(function(l){return Object.getOwnPropertyDescriptor(e,l).enumerable})),s.push.apply(s,t)}return s}function x(e){for(var n=1;n<arguments.length;n++){var s=arguments[n]!=null?arguments[n]:{};n%2?D(Object(s),!0).forEach(function(t){ke(e,t,s[t])}):Object.getOwnPropertyDescriptors?Object.defineProperties(e,Object.getOwnPropertyDescriptors(s)):D(Object(s)).forEach(function(t){Object.defineProperty(e,t,Object.getOwnPropertyDescriptor(s,t))})}return e}function ke(e,n,s){return(n=Ce(n))in e?Object.defineProperty(e,n,{value:s,enumerable:!0,configurable:!0,writable:!0}):e[n]=s,e}function Ce(e){var n=Re(e,"string");return h(n)=="symbol"?n:n+""}function Re(e,n){if(h(e)!="object"||!e)return e;var s=e[Symbol.toPrimitive];if(s!==void 0){var t=s.call(e,n);if(h(t)!="object")return t;throw new TypeError("@@toPrimitive must return a primitive value.")}return(n==="string"?String:Number)(e)}var ze=["data-p"],Ne=["data-p"],$e=["data-p"],De=["aria-label","data-p"],xe=["data-p"];function Me(e,n,s,t,l,a){var c=K("TimesIcon"),k=H("ripple");return o(),f(F,r({name:"p-message",appear:""},e.ptmi("transition")),{default:w(function(){return[C(u("div",r({class:e.cx("root"),role:"alert","aria-live":"assertive","aria-atomic":"true","data-p":a.dataP},e.ptm("root")),[e.$slots.container?p(e.$slots,"container",{key:0,closeCallback:a.close}):(o(),i("div",r({key:1,class:e.cx("content"),"data-p":a.dataP},e.ptm("content")),[p(e.$slots,"icon",{class:M(e.cx("icon"))},function(){return[(o(),f(L(e.icon?"span":null),r({class:[e.cx("icon"),e.icon],"data-p":a.dataP},e.ptm("icon")),null,16,["class","data-p"]))]}),e.$slots.default?(o(),i("div",r({key:0,class:e.cx("text"),"data-p":a.dataP},e.ptm("text")),[p(e.$slots,"default")],16,$e)):A("",!0),e.closable?C((o(),i("button",r({key:1,class:e.cx("closeButton"),"aria-label":a.closeAriaLabel,type:"button",onClick:n[0]||(n[0]=function(V){return a.close(V)}),"data-p":a.dataP},x(x({},e.closeButtonProps),e.ptm("closeButton"))),[p(e.$slots,"closeicon",{},function(){return[e.closeIcon?(o(),i("i",r({key:0,class:[e.cx("closeIcon"),e.closeIcon],"data-p":a.dataP},e.ptm("closeIcon")),null,16,xe)):(o(),f(c,r({key:1,class:[e.cx("closeIcon"),e.closeIcon],"data-p":a.dataP},e.ptm("closeIcon")),null,16,["class","data-p"]))]})],16,De)),[[k]]):A("",!0)],16,Ne))],16,ze),[[W,l.visible]])]}),_:3},16)}B.render=Me;var Le=`
    .p-toolbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        flex-wrap: wrap;
        padding: dt('toolbar.padding');
        background: dt('toolbar.background');
        border: 1px solid dt('toolbar.border.color');
        color: dt('toolbar.color');
        border-radius: dt('toolbar.border.radius');
        gap: dt('toolbar.gap');
    }

    .p-toolbar-start,
    .p-toolbar-center,
    .p-toolbar-end {
        display: flex;
        align-items: center;
    }
`,Ue={root:"p-toolbar p-component",start:"p-toolbar-start",center:"p-toolbar-center",end:"p-toolbar-end"},Ye=O.extend({name:"toolbar",style:Le,classes:Ue}),Ie={name:"BaseToolbar",extends:S,props:{ariaLabelledby:{type:String,default:null}},style:Ye,provide:function(){return{$pcToolbar:this,$parentInstance:this}}},j={name:"Toolbar",extends:Ie,inheritAttrs:!1},Be=["aria-labelledby"];function je(e,n,s,t,l,a){return o(),i("div",r({class:e.cx("root"),role:"toolbar","aria-labelledby":e.ariaLabelledby},e.ptmi("root")),[u("div",r({class:e.cx("start")},e.ptm("start")),[p(e.$slots,"start")],16),u("div",r({class:e.cx("center")},e.ptm("center")),[p(e.$slots,"center")],16),u("div",r({class:e.cx("end")},e.ptm("end")),[p(e.$slots,"end")],16)],16,Be)}j.render=je;const E=J({Skills:{},Features:{},Bodies:{},Items:{},SkillTrees:{},Midia:{}});var b=(e=>(e[e.HANDSHAKE=0]="HANDSHAKE",e[e.DISCONNECT=1]="DISCONNECT",e[e.CHAT=2]="CHAT",e[e.BOARD_ADD=3]="BOARD_ADD",e[e.BOARD_REMOVE=4]="BOARD_REMOVE",e[e.FLOOR_IMAGE=5]="FLOOR_IMAGE",e[e.DOOR_UPDATE=6]="DOOR_UPDATE",e[e.DOOR_INTERACT=7]="DOOR_INTERACT",e[e.COMBAT_MODE=8]="COMBAT_MODE",e[e.ENTITY_CREATE=9]="ENTITY_CREATE",e[e.ENTITY_MIDIA=10]="ENTITY_MIDIA",e[e.ENTITY_REMOVE=11]="ENTITY_REMOVE",e[e.ENTITY_MOVE=12]="ENTITY_MOVE",e[e.ENTITY_POSITION=13]="ENTITY_POSITION",e[e.ENTITY_ROTATION=14]="ENTITY_ROTATION",e[e.ENTITY_VELOCITY=15]="ENTITY_VELOCITY",e[e.ENTITY_BODY_PART=16]="ENTITY_BODY_PART",e[e.ENTITY_BODY_PART_INJURY=17]="ENTITY_BODY_PART_INJURY",e[e.ENTITY_STAT=18]="ENTITY_STAT",e[e.FEATURE_UPDATE=19]="FEATURE_UPDATE",e[e.CREATURE_EQUIP_ITEM=20]="CREATURE_EQUIP_ITEM",e[e.CREATURE_SKILL_UPDATE=21]="CREATURE_SKILL_UPDATE",e[e.CREATURE_SKILL_REMOVE=22]="CREATURE_SKILL_REMOVE",e[e.CREATURE_ACTION_LAYER_UPDATE=23]="CREATURE_ACTION_LAYER_UPDATE",e[e.CREATURE_ACTION_LAYER_REMOVE=24]="CREATURE_ACTION_LAYER_REMOVE",e[e.CREATURE_SKILLTREE_UPDATE=25]="CREATURE_SKILLTREE_UPDATE",e[e.EXECUTE_COMMAND=26]="EXECUTE_COMMAND",e[e.COMPENDIUM_UPDATE=27]="COMPENDIUM_UPDATE",e[e.SHOW_MIDIA=28]="SHOW_MIDIA",e))(b||{}),P=(e=>(e[e.DESKTOP=0]="DESKTOP",e[e.MOBILE=1]="MOBILE",e))(P||{});let m;function Pe(){m=new WebSocket("ws://"+window.location.host+"/ws"),m.onopen=()=>{console.log("WebSocket connection established."),console.log(m.readyState),m.send(JSON.stringify({id:b.HANDSHAKE,username:d.username,device:P.MOBILE}))},m.onerror=e=>{console.log("WebSocket error: ",e)},m.onclose=e=>{console.log("WebSocket connection closed: ",e)},m.onmessage=e=>{const n=JSON.parse(e.data);switch(n.id){case b.COMPENDIUM_UPDATE:{const s=n,t=s.registryName,l=s.dataName,a=s.json;s.remove?delete E[t][l]:(E[t]||(E[t]={}),E[t][l]=a);break}case b.ENTITY_CREATE:{const s=n;d.entities.push(s.entity);break}case b.ENTITY_REMOVE:{const s=n;d.entities=d.entities.filter(t=>t.id!==s.ref.id);break}case b.ENTITY_STAT:{const s=n,t=d.entities.find(l=>l.id===s.entityRef.id);t&&(t.stats[s.statId]=s.stat)}default:{console.warn("Unhandled packet id:",n.id);break}}}}const Ve={class:"min-h-screen flex flex-col"},Ke={class:"flex items-center gap-4 overflow-x-auto"},He={class:"text-lg font-semibold whitespace-nowrap"},We={key:1,class:"flex items-center gap-2"},Fe={class:"flex-1 container mx-auto p-4"},Je={key:0,class:"rounded-lg border border-gray-200 p-4"},qe={class:"text-xl font-semibold mb-2"},Ge={key:1,class:"text-2xl font-bold"},Ze=q({__name:"IndexPage",setup(e){const n=R(""),s=R(null);function t(){d.setUsername(""),window.location.reload()}return G(()=>{d.username||window.location.reload(),Pe()}),(l,a)=>(o(),i("div",Ve,[z(g(j),{class:"px-4"},{start:w(()=>[u("div",Ke,[u("h1",He,"Bem-vindo, "+T(g(d).username),1),n.value?(o(),f(g(B),{key:0,severity:"error",class:"!m-0"},{default:w(()=>[Q("Erro: "+T(n.value),1)]),_:1})):(o(),i("div",We,[(o(!0),i(X,null,Z(g(d).entities,c=>(o(),f(g(I),{key:c.id,shape:"circle",size:"large",label:c.display?void 0:c.name?.charAt(0).toUpperCase()??"?",image:c.display?"data:image/unknown;base64,"+c.display:void 0,style:ee({backgroundColor:s.value&&s.value.id===c.id?"#dc2626":"#9ca3af",color:"white",cursor:"pointer",boxShadow:s.value&&s.value.id===c.id?"0 0 0 3px rgba(220,38,38,0.5)":"none"}),title:c.name,onClick:k=>s.value=c},null,8,["label","image","style","title","onClick"]))),128))]))])]),end:w(()=>[z(g(te),{label:"Sair",onClick:t})]),_:1}),u("main",Fe,[s.value?(o(),i("div",Je,[u("h3",qe,T(s.value.name),1)])):(o(),i("h2",Ge,"Selecione um personagem"))])]))}});export{Ze as default};
